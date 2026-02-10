"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import {
    Lightbulb,
    LayoutGrid,
    Palette,
    Film,
    PlayCircle,
    Radio,
    AlertCircle,
    ExternalLink,
} from "lucide-react";
import { CopyButton } from "./CopyButton";

const clients = ["Claude Desktop", "Claude Code", "VS Code"] as const;
type Client = (typeof clients)[number];

const claudeDesktopConfig = `{
  "mcpServers": {
    "hue-companion": {
      "command": "C:\\\\Program Files\\\\HueCompanion\\\\HueCompanion.Mcp.exe",
      "args": ["--stdio"]
    }
  }
}`;

const claudeCodeConfig = `{
  "mcpServers": {
    "hue-companion": {
      "command": "C:\\\\Program Files\\\\HueCompanion\\\\HueCompanion.Mcp.exe",
      "args": ["--stdio"]
    }
  }
}`;

const vsCodeConfig = `{
  "mcp": {
    "servers": {
      "hue-companion": {
        "url": "https://localhost:5680"
      }
    }
  }
}`;

const capabilities = [
    {
        icon: Lightbulb,
        title: "Control Lights",
        description:
            "Turn lights on/off, set brightness, change colors, and adjust color temperature.",
    },
    {
        icon: LayoutGrid,
        title: "Manage Rooms & Zones",
        description:
            "Control entire rooms or zones at once. List all rooms and their current states.",
    },
    {
        icon: Palette,
        title: "Activate Scenes",
        description:
            "Browse and activate saved scenes including both built-in and custom scenes.",
    },
    {
        icon: Film,
        title: "Create Animated Scenes",
        description:
            "Build keyframe-based animated scenes with per-light tracks and easing options.",
    },
    {
        icon: PlayCircle,
        title: "Play Animations",
        description:
            "Start, stop, and manage animated scene playback in any room or zone.",
    },
    {
        icon: Radio,
        title: "Bridge Status",
        description:
            "View connected Hue bridges, their status, and the lights available on each.",
    },
];

const examplePrompts = [
    "Turn off all the lights in the bedroom",
    "Set the living room to a warm sunset mood",
    "Create an animated scene that slowly cycles through ocean colors",
    "What lights are currently on?",
    "Set the office to 50% brightness with a cool white tone",
    "Play the 'Chill Evening' scene in the lounge",
];

export function McpSetupContent() {
    const [activeClient, setActiveClient] = useState<Client>("Claude Desktop");

    return (
        <div className="max-w-3xl mx-auto px-6 pt-32 pb-20">
            {/* Header */}
            <header className="mb-20 border-b border-zinc-800 pb-10">
                <motion.h1
                    initial={{ opacity: 0, y: 20 }}
                    animate={{ opacity: 1, y: 0 }}
                    className="text-4xl md:text-5xl font-bold text-white mb-6"
                >
                    MCP Server Setup
                </motion.h1>
                <motion.p
                    initial={{ opacity: 0, y: 20 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: 0.1 }}
                    className="text-xl text-zinc-400 leading-relaxed"
                >
                    Connect Hue Companion to Claude and other AI assistants
                    using the{" "}
                    <a
                        href="https://modelcontextprotocol.io"
                        target="_blank"
                        rel="noopener noreferrer"
                        className="text-zinc-200 underline underline-offset-4 decoration-zinc-600 hover:decoration-zinc-400 transition-colors"
                    >
                        Model Context Protocol
                    </a>
                    .
                </motion.p>
            </header>

            <article className="prose prose-invert prose-zinc max-w-none">
                {/* What You Can Do */}
                <section className="mb-16">
                    <h2 className="text-2xl font-bold text-white mb-6">
                        What You Can Do
                    </h2>
                    <p className="text-zinc-300 mb-8">
                        Once connected, your AI assistant gets full control over
                        your Philips Hue lighting system through natural
                        language.
                    </p>
                    <dl className="grid gap-6 md:grid-cols-2">
                        {capabilities.map((cap, i) => (
                            <motion.div
                                key={cap.title}
                                initial={{ opacity: 0, y: 20 }}
                                whileInView={{ opacity: 1, y: 0 }}
                                transition={{ delay: i * 0.05 }}
                                viewport={{ once: true }}
                                className="bg-zinc-900/50 p-6 rounded-xl border border-zinc-800"
                            >
                                <dt className="text-white font-bold mb-2 flex items-center gap-2">
                                    <cap.icon className="w-4 h-4 text-zinc-400" />
                                    {cap.title}
                                </dt>
                                <dd className="text-zinc-400 text-sm">
                                    {cap.description}
                                </dd>
                            </motion.div>
                        ))}
                    </dl>
                </section>

                {/* Prerequisites */}
                <section className="mb-16">
                    <h2 className="text-2xl font-bold text-white mb-6">
                        Prerequisites
                    </h2>
                    <ul className="space-y-3 text-zinc-300">
                        <li className="flex items-start gap-3">
                            <span className="mt-1 h-5 w-5 rounded-full bg-zinc-800 border border-zinc-700 flex items-center justify-center text-xs text-zinc-400 shrink-0">
                                1
                            </span>
                            <span>
                                <strong className="text-white">
                                    Hue Companion
                                </strong>{" "}
                                installed on your Windows PC
                            </span>
                        </li>
                        <li className="flex items-start gap-3">
                            <span className="mt-1 h-5 w-5 rounded-full bg-zinc-800 border border-zinc-700 flex items-center justify-center text-xs text-zinc-400 shrink-0">
                                2
                            </span>
                            <span>
                                <strong className="text-white">
                                    MCP server enabled
                                </strong>{" "}
                                in Hue Companion settings (Settings &rarr; MCP
                                Server &rarr; toggle on)
                            </span>
                        </li>
                        <li className="flex items-start gap-3">
                            <span className="mt-1 h-5 w-5 rounded-full bg-zinc-800 border border-zinc-700 flex items-center justify-center text-xs text-zinc-400 shrink-0">
                                3
                            </span>
                            <span>
                                A supported AI client:{" "}
                                <strong className="text-white">
                                    Claude Desktop
                                </strong>
                                ,{" "}
                                <strong className="text-white">
                                    Claude Code
                                </strong>
                                , or{" "}
                                <strong className="text-white">
                                    VS Code
                                </strong>{" "}
                                with Copilot
                            </span>
                        </li>
                    </ul>
                </section>

                {/* Setup Instructions */}
                <section className="mb-16">
                    <h2 className="text-2xl font-bold text-white mb-6">
                        Setup Instructions
                    </h2>

                    {/* Client Tabs */}
                    <div className="flex gap-2 mb-8">
                        {clients.map((client) => (
                            <button
                                key={client}
                                onClick={() => setActiveClient(client)}
                                className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                                    activeClient === client
                                        ? "bg-zinc-800 text-white border border-zinc-700"
                                        : "text-zinc-400 hover:text-zinc-200 border border-transparent"
                                }`}
                            >
                                {client}
                            </button>
                        ))}
                    </div>

                    {/* Claude Desktop Instructions */}
                    {activeClient === "Claude Desktop" && (
                        <div className="space-y-6">
                            <Step number={1}>
                                <p className="text-zinc-300">
                                    Open the Claude Desktop config file. You can
                                    find it at:
                                </p>
                                <CodePath>
                                    %APPDATA%\Claude\claude_desktop_config.json
                                </CodePath>
                                <p className="text-zinc-400 text-sm mt-2">
                                    If the file doesn&apos;t exist, create it.
                                </p>
                            </Step>

                            <Step number={2}>
                                <p className="text-zinc-300 mb-4">
                                    Add the following to the config file. If the
                                    file already has content, merge the{" "}
                                    <code className="text-zinc-200 bg-zinc-800 px-1.5 py-0.5 rounded text-sm">
                                        mcpServers
                                    </code>{" "}
                                    entry into the existing object.
                                </p>
                                <ConfigBlock
                                    config={claudeDesktopConfig}
                                    label="Copy config"
                                />
                            </Step>

                            <Step number={3}>
                                <p className="text-zinc-300">
                                    Restart Claude Desktop for the changes to
                                    take effect.
                                </p>
                            </Step>

                            <PathNote />
                        </div>
                    )}

                    {/* Claude Code Instructions */}
                    {activeClient === "Claude Code" && (
                        <div className="space-y-6">
                            <Step number={1}>
                                <p className="text-zinc-300">
                                    In your project root, create or open the MCP
                                    config file:
                                </p>
                                <CodePath>.mcp.json</CodePath>
                            </Step>

                            <Step number={2}>
                                <p className="text-zinc-300 mb-4">
                                    Add the Hue Companion server configuration:
                                </p>
                                <ConfigBlock
                                    config={claudeCodeConfig}
                                    label="Copy config"
                                />
                            </Step>

                            <Step number={3}>
                                <p className="text-zinc-300">
                                    Restart Claude Code. The MCP tools will be
                                    available automatically.
                                </p>
                            </Step>

                            <PathNote />
                        </div>
                    )}

                    {/* VS Code Instructions */}
                    {activeClient === "VS Code" && (
                        <div className="space-y-6">
                            <Step number={1}>
                                <p className="text-zinc-300">
                                    Make sure Hue Companion is running with the
                                    MCP server enabled. VS Code connects over
                                    HTTP, so the app must be open.
                                </p>
                            </Step>

                            <Step number={2}>
                                <p className="text-zinc-300">
                                    In your project, create or open:
                                </p>
                                <CodePath>.vscode/mcp.json</CodePath>
                            </Step>

                            <Step number={3}>
                                <p className="text-zinc-300 mb-4">
                                    Add the Hue Companion server configuration:
                                </p>
                                <ConfigBlock
                                    config={vsCodeConfig}
                                    label="Copy config"
                                />
                            </Step>

                            <Step number={4}>
                                <p className="text-zinc-300">
                                    Reload VS Code. The Hue Companion tools will
                                    appear in Copilot&apos;s tool list.
                                </p>
                            </Step>
                        </div>
                    )}
                </section>

                {/* Tip: In-app copy */}
                <section className="mb-16">
                    <div className="bg-zinc-900/50 border border-zinc-800 rounded-xl p-6 flex gap-4">
                        <AlertCircle className="w-5 h-5 text-zinc-400 shrink-0 mt-0.5" />
                        <div>
                            <p className="text-zinc-200 font-medium mb-1">
                                Easier way: copy from the app
                            </p>
                            <p className="text-zinc-400 text-sm">
                                Hue Companion&apos;s Settings page has copy
                                buttons that generate the config with the
                                correct executable path for your system. Go to{" "}
                                <strong className="text-zinc-300">
                                    Settings &rarr; MCP Server
                                </strong>{" "}
                                and use the copy button for your AI client.
                            </p>
                        </div>
                    </div>
                </section>

                {/* Example Prompts */}
                <section className="mb-16">
                    <h2 className="text-2xl font-bold text-white mb-6">
                        Example Prompts
                    </h2>
                    <p className="text-zinc-300 mb-6">
                        Once connected, try asking your AI assistant things
                        like:
                    </p>
                    <div className="grid gap-3 md:grid-cols-2">
                        {examplePrompts.map((prompt, i) => (
                            <motion.div
                                key={i}
                                initial={{ opacity: 0, y: 10 }}
                                whileInView={{ opacity: 1, y: 0 }}
                                transition={{ delay: i * 0.05 }}
                                viewport={{ once: true }}
                                className="bg-zinc-900/50 border border-zinc-800 rounded-lg px-4 py-3 text-sm text-zinc-300 italic"
                            >
                                &ldquo;{prompt}&rdquo;
                            </motion.div>
                        ))}
                    </div>
                </section>

                {/* Troubleshooting */}
                <section>
                    <h2 className="text-2xl font-bold text-white mb-6">
                        Troubleshooting
                    </h2>
                    <div className="space-y-8">
                        <div>
                            <h3 className="text-lg font-bold text-white mb-2">
                                &ldquo;Server not found&rdquo; or connection
                                error
                            </h3>
                            <p className="text-zinc-400">
                                Verify the executable path in your config file
                                is correct. Open Hue Companion and go to
                                Settings &rarr; MCP Server to copy the config
                                with the auto-detected path. For VS Code, make
                                sure Hue Companion is running.
                            </p>
                        </div>
                        <div>
                            <h3 className="text-lg font-bold text-white mb-2">
                                Lights not responding
                            </h3>
                            <p className="text-zinc-400">
                                Check that your Hue Bridge is connected in Hue
                                Companion. The MCP server uses the same bridge
                                connections as the app. Go to Settings &rarr;
                                Hue Bridge to verify your bridge is connected.
                            </p>
                        </div>
                        <div>
                            <h3 className="text-lg font-bold text-white mb-2">
                                MCP server not starting
                            </h3>
                            <p className="text-zinc-400">
                                Make sure the MCP server toggle is enabled in
                                Hue Companion&apos;s settings. The server starts
                                automatically when the app launches (if
                                enabled). Try toggling it off and on again.
                            </p>
                        </div>
                        <div>
                            <h3 className="text-lg font-bold text-white mb-2">
                                Changes to config not taking effect
                            </h3>
                            <p className="text-zinc-400">
                                Claude Desktop and Claude Code require a restart
                                after editing their config files. VS Code
                                requires a window reload (Ctrl+Shift+P &rarr;
                                &ldquo;Reload Window&rdquo;).
                            </p>
                        </div>
                    </div>
                </section>
            </article>
        </div>
    );
}

function Step({
    number,
    children,
}: {
    number: number;
    children: React.ReactNode;
}) {
    return (
        <div className="flex gap-4">
            <span className="mt-1 h-6 w-6 rounded-full bg-zinc-800 border border-zinc-700 flex items-center justify-center text-xs text-zinc-300 font-medium shrink-0">
                {number}
            </span>
            <div className="flex-1">{children}</div>
        </div>
    );
}

function CodePath({ children }: { children: React.ReactNode }) {
    return (
        <code className="block mt-2 bg-zinc-900 border border-zinc-800 rounded-lg px-4 py-2.5 text-sm text-zinc-200 font-mono">
            {children}
        </code>
    );
}

function ConfigBlock({ config, label }: { config: string; label: string }) {
    return (
        <div className="relative">
            <pre className="bg-zinc-900 border border-zinc-800 rounded-lg p-4 text-sm text-zinc-300 font-mono overflow-x-auto">
                {config}
            </pre>
            <div className="absolute top-3 right-3">
                <CopyButton text={config} label={label} />
            </div>
        </div>
    );
}

function PathNote() {
    return (
        <div className="bg-zinc-900/30 border border-zinc-800/50 rounded-lg p-4 flex gap-3 text-sm">
            <AlertCircle className="w-4 h-4 text-zinc-500 shrink-0 mt-0.5" />
            <p className="text-zinc-400">
                The path above is a placeholder. Your actual install path may
                differ. For the correct path, use the{" "}
                <strong className="text-zinc-300">copy button</strong> in Hue
                Companion&apos;s Settings &rarr; MCP Server section, which
                auto-detects it.
            </p>
        </div>
    );
}
