import test from 'node:test';
import assert from 'node:assert/strict';
import { buildArguments } from './build-local.mjs';

test('local builds select explicit campaign entry and platform', () => {
  const windows = buildArguments('windows', 'project with spaces', 'build.log');
  assert.equal(windows[windows.indexOf('-executeMethod') + 1], 'CampaignLocalBuild.Windows');
  assert.equal(windows[windows.indexOf('-projectPath') + 1], 'project with spaces');
  const android = buildArguments('android', 'project', 'build.log');
  assert.equal(android[android.indexOf('-buildTarget') + 1], 'Android');
  assert.equal(android[android.indexOf('-executeMethod') + 1], 'CampaignLocalBuild.Android');
  assert.throws(() => buildArguments('ios', 'project', 'build.log'), /Unsupported/);
});
