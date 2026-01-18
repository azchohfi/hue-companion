/**
 * Hue Companion Screenshot MCP Server
 *
 * Provides tools for Claude to interact with app screenshots:
 * - list_screenshots: List available screenshots
 * - get_screenshot: Get a specific screenshot by filename
 * - get_latest_screenshot: Get the most recent screenshot
 * - capture_screenshot: Trigger a new screenshot capture
 */

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from '@modelcontextprotocol/sdk/types.js';
import fs from 'fs';
import path from 'path';
import { execSync } from 'child_process';
import { fileURLToPath } from 'url';

// Get directory paths
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const PROJECT_ROOT = path.resolve(__dirname, '..', '..');
const SCREENSHOTS_DIR = path.join(PROJECT_ROOT, 'screenshots');
const CAPTURE_SCRIPT = path.join(PROJECT_ROOT, 'tools', 'Capture-AppScreenshot.ps1');

// Create server instance
const server = new Server(
  {
    name: 'hue-screenshot-server',
    version: '1.0.0',
  },
  {
    capabilities: {
      tools: {},
    },
  }
);

// Helper: Get sorted list of screenshots
function getScreenshotFiles(limit = 100) {
  if (!fs.existsSync(SCREENSHOTS_DIR)) {
    return [];
  }

  return fs.readdirSync(SCREENSHOTS_DIR)
    .filter(f => f.endsWith('.png'))
    .map(f => {
      const filepath = path.join(SCREENSHOTS_DIR, f);
      const stats = fs.statSync(filepath);
      return {
        filename: f,
        path: filepath,
        modified: stats.mtime,
        size: stats.size,
      };
    })
    .sort((a, b) => b.modified - a.modified)
    .slice(0, limit);
}

// Helper: Read screenshot as base64
function readScreenshotBase64(filepath) {
  const imageData = fs.readFileSync(filepath);
  return imageData.toString('base64');
}

// Register tool list handler
server.setRequestHandler(ListToolsRequestSchema, async () => {
  return {
    tools: [
      {
        name: 'list_screenshots',
        description: 'List all captured screenshots of the Hue Companion app with timestamps and file sizes',
        inputSchema: {
          type: 'object',
          properties: {
            limit: {
              type: 'number',
              description: 'Maximum number of screenshots to return (default: 10)',
            },
          },
        },
      },
      {
        name: 'get_screenshot',
        description: 'Get a specific screenshot by filename. Returns the image for visual analysis.',
        inputSchema: {
          type: 'object',
          properties: {
            filename: {
              type: 'string',
              description: 'The screenshot filename (e.g., "hue-20260113-153045.png")',
            },
          },
          required: ['filename'],
        },
      },
      {
        name: 'get_latest_screenshot',
        description: 'Get the most recent screenshot of the Hue Companion app. Returns the image for visual analysis.',
        inputSchema: {
          type: 'object',
          properties: {},
        },
      },
      {
        name: 'capture_screenshot',
        description: 'Build and launch the Hue Companion app, then capture a screenshot of the window. Returns the captured image.',
        inputSchema: {
          type: 'object',
          properties: {
            skipBuild: {
              type: 'boolean',
              description: 'Skip the build step - just launch the existing executable and capture',
            },
            waitSeconds: {
              type: 'number',
              description: 'Seconds to wait after launch before capturing (default: 5)',
            },
          },
        },
      },
    ],
  };
});

// Register tool call handler
server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  switch (name) {
    case 'list_screenshots': {
      const limit = args?.limit || 10;
      const files = getScreenshotFiles(limit);

      if (files.length === 0) {
        return {
          content: [
            {
              type: 'text',
              text: 'No screenshots found. Use capture_screenshot to capture the app.',
            },
          ],
        };
      }

      const fileList = files.map(f => ({
        filename: f.filename,
        timestamp: f.modified.toISOString(),
        size: `${Math.round(f.size / 1024)} KB`,
      }));

      return {
        content: [
          {
            type: 'text',
            text: `Found ${files.length} screenshot(s):\n\n${JSON.stringify(fileList, null, 2)}`,
          },
        ],
      };
    }

    case 'get_screenshot': {
      const filename = args?.filename;
      if (!filename) {
        return {
          content: [{ type: 'text', text: 'Error: filename is required' }],
          isError: true,
        };
      }

      const filepath = path.join(SCREENSHOTS_DIR, filename);
      if (!fs.existsSync(filepath)) {
        return {
          content: [{ type: 'text', text: `Screenshot not found: ${filename}` }],
          isError: true,
        };
      }

      const base64 = readScreenshotBase64(filepath);
      return {
        content: [
          { type: 'text', text: `Screenshot: ${filename}` },
          { type: 'image', data: base64, mimeType: 'image/png' },
        ],
      };
    }

    case 'get_latest_screenshot': {
      const files = getScreenshotFiles(1);

      if (files.length === 0) {
        return {
          content: [
            {
              type: 'text',
              text: 'No screenshots found. Use capture_screenshot to capture the app.',
            },
          ],
        };
      }

      const latest = files[0];
      const base64 = readScreenshotBase64(latest.path);

      return {
        content: [
          {
            type: 'text',
            text: `Latest screenshot: ${latest.filename} (captured ${latest.modified.toISOString()})`,
          },
          { type: 'image', data: base64, mimeType: 'image/png' },
        ],
      };
    }

    case 'capture_screenshot': {
      const skipBuild = args?.skipBuild ? '-SkipBuild' : '';
      const waitSeconds = args?.waitSeconds ? `-WaitSeconds ${args.waitSeconds}` : '';

      try {
        // Run the PowerShell capture script
        const command = `powershell -ExecutionPolicy Bypass -File "${CAPTURE_SCRIPT}" ${skipBuild} ${waitSeconds}`.trim();

        execSync(command, {
          cwd: PROJECT_ROOT,
          stdio: 'pipe',
          timeout: 120000, // 2 minute timeout
        });

        // Get the newly captured screenshot
        const files = getScreenshotFiles(1);
        if (files.length === 0) {
          return {
            content: [
              {
                type: 'text',
                text: 'Capture completed but no screenshot was found. Check if the app launched correctly.',
              },
            ],
          };
        }

        const latest = files[0];
        const base64 = readScreenshotBase64(latest.path);

        return {
          content: [
            {
              type: 'text',
              text: `Screenshot captured: ${latest.filename}`,
            },
            { type: 'image', data: base64, mimeType: 'image/png' },
          ],
        };
      } catch (error) {
        return {
          content: [
            {
              type: 'text',
              text: `Capture failed: ${error.message}\n\nMake sure the app can build and launch successfully.`,
            },
          ],
          isError: true,
        };
      }
    }

    default:
      return {
        content: [{ type: 'text', text: `Unknown tool: ${name}` }],
        isError: true,
      };
  }
});

// Start the server
async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error('Hue Companion Screenshot MCP Server running on stdio');
}

main().catch((error) => {
  console.error('Server error:', error);
  process.exit(1);
});
