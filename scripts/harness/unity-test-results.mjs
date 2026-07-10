import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';

function parseAttributes(value) {
  return Object.fromEntries(
    [...value.matchAll(/([\w-]+)="([^"]*)"/g)].map((match) => [match[1], match[2]])
  );
}

function parseXmlRoot(xml) {
  const stack = [];
  let root = null;
  let index = 0;
  while (index < xml.length) {
    if (xml[index] !== '<') {
      const next = xml.indexOf('<', index);
      const end = next === -1 ? xml.length : next;
      if (stack.length === 0 && xml.slice(index, end).trim()) return null;
      index = end;
      continue;
    }
    if (xml.startsWith('<!--', index)) {
      const end = xml.indexOf('-->', index + 4);
      if (end === -1) return null;
      index = end + 3;
      continue;
    }
    if (xml.startsWith('<![CDATA[', index)) {
      if (stack.length === 0) return null;
      const end = xml.indexOf(']]>', index + 9);
      if (end === -1) return null;
      index = end + 3;
      continue;
    }
    if (xml.startsWith('<?', index)) {
      if (root || stack.length > 0) return null;
      const end = xml.indexOf('?>', index + 2);
      if (end === -1) return null;
      index = end + 2;
      continue;
    }
    let end = index + 1;
    let quote = null;
    for (; end < xml.length; end += 1) {
      const character = xml[end];
      if (quote) {
        if (character === quote) quote = null;
      } else if (character === '"' || character === "'") {
        quote = character;
      } else if (character === '>') {
        break;
      }
    }
    if (end >= xml.length || quote) return null;
    const tag = xml.slice(index + 1, end).trim();
    if (!tag || tag.startsWith('!')) return null;
    if (tag.startsWith('/')) {
      const name = tag.slice(1).trim();
      if (!/^[A-Za-z_][\w:.-]*$/.test(name) || stack.pop() !== name) return null;
    } else {
      const selfClosing = tag.endsWith('/');
      const body = selfClosing ? tag.slice(0, -1).trim() : tag;
      const match = /^([A-Za-z_][\w:.-]*)(?:\s+([\s\S]*))?$/.exec(body);
      if (!match || (stack.length === 0 && root)) return null;
      if (!root) root = { name: match[1], attributes: match[2] ?? '', selfClosing };
      if (!selfClosing) stack.push(match[1]);
    }
    index = end + 1;
  }
  return stack.length === 0 ? root : null;
}

export function parseUnityTestResults(xml) {
  const root = parseXmlRoot(xml);
  if (root?.name.toLowerCase() !== 'test-run' || root.selfClosing) {
    return { status: 'failed', result: null, total: 0, passed: 0, failed: 0, skipped: 0 };
  }
  const attributes = parseAttributes(root.attributes);
  const total = Number(attributes.testcasecount ?? attributes.total ?? 0);
  const passed = Number(attributes.passed ?? 0);
  const directFailed = Number(attributes.failed ?? 0);
  const inconclusive = Number(attributes.inconclusive ?? 0);
  const failed = directFailed + inconclusive;
  const skipped = Number(attributes.skipped ?? 0);
  const result = attributes.result ?? null;
  const counts = [total, passed, directFailed, inconclusive, skipped];
  const countsAreValid = counts.every((value) => Number.isInteger(value) && value >= 0);
  const countsAreConsistent = countsAreValid && passed + directFailed + inconclusive + skipped === total;
  const status = result === 'Passed' && total > 0 && passed === total && failed === 0 && skipped === 0 && countsAreConsistent
    ? 'passed'
    : 'failed';
  return { status, result, total, passed, failed, skipped };
}

export function findUnityResultFiles(directory) {
  if (!existsSync(directory)) return [];
  const files = [];
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = resolve(directory, entry.name);
    if (entry.isDirectory()) files.push(...findUnityResultFiles(path));
    else if (entry.isFile() && entry.name.toLowerCase().endsWith('.xml')) files.push(path);
  }
  return files.sort();
}

export function summarizeUnityResultDirectory(directory) {
  const files = findUnityResultFiles(directory);
  const results = files.map((path) => ({ path, ...parseUnityTestResults(readFileSync(path, 'utf8')) }));
  const total = results.reduce((sum, result) => sum + result.total, 0);
  const passed = results.reduce((sum, result) => sum + result.passed, 0);
  const failed = results.reduce((sum, result) => sum + result.failed, 0);
  return {
    status: results.length > 0 && results.every((result) => result.status === 'passed') && total > 0
      ? 'passed'
      : 'failed',
    files,
    total,
    passed,
    failed
  };
}
