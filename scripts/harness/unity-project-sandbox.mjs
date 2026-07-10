import { cpSync, mkdirSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const PROJECT_DIRECTORIES = ['Assets', 'Packages', 'ProjectSettings'];
const RETRYABLE_CLEANUP_ERRORS = new Set(['EBUSY', 'ENOTEMPTY', 'EPERM']);

function sleep(milliseconds) {
  Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, milliseconds);
}

export function removeUnityProjectSandbox(sandboxRoot, timeoutMs = 120_000) {
  const deadline = Date.now() + timeoutMs;
  for (;;) {
    try {
      rmSync(sandboxRoot, { recursive: true, force: true });
      return;
    } catch (error) {
      if (!RETRYABLE_CLEANUP_ERRORS.has(error.code) || Date.now() >= deadline) throw error;
      sleep(1_000);
    }
  }
}

export function createUnityProjectSandbox(root) {
  const sandboxRoot = mkdtempSync(join(tmpdir(), 'armada-unity-'));
  const projectPath = resolve(sandboxRoot, 'unity');
  mkdirSync(projectPath, { recursive: true });
  try {
    for (const directory of PROJECT_DIRECTORIES) {
      cpSync(resolve(root, 'unity', directory), resolve(projectPath, directory), {
        recursive: true,
        force: true
      });
    }
  } catch (error) {
    rmSync(sandboxRoot, { recursive: true, force: true });
    throw error;
  }
  return {
    projectPath,
    cleanup() {
      // Unity package-manager children can retain a Windows directory handle briefly
      // after the Editor exits. Keep cleanup bounded, but allow that handle to drain.
      removeUnityProjectSandbox(sandboxRoot);
    }
  };
}
