const fs = require('fs');
const path = require('path');

// Read package.json
const packageJson = JSON.parse(
  fs.readFileSync(path.join(__dirname, '../package.json'), 'utf8')
);

// Create version.ts content
const versionFileContent = `export const version = '${packageJson.version}';\n`;

// Write to version.ts
fs.writeFileSync(
  path.join(__dirname, '../src/app/version.ts'),
  versionFileContent
);

console.log(`✓ Version updated to ${packageJson.version}`);
