import { Footer } from "@/components/Footer";
import { McpSetupContent } from "@/components/McpSetupContent";
import { Metadata } from "next";
import {
    SITE_URL,
    SITE_NAME,
    sharedOpenGraph,
    sharedTwitter,
    breadcrumbJsonLd,
    canonicalUrl,
} from "@/lib/seo";

export const metadata: Metadata = {
    title: "MCP Server Setup",
    description: "Set up the Hue Companion MCP server to control your Philips Hue lights from Claude Desktop, Claude Code, VS Code Copilot, and other AI assistants. The easiest way to control Hue lights via MCP on Windows.",
    openGraph: {
        ...sharedOpenGraph,
        title: `MCP Server Setup — ${SITE_NAME}`,
        description: "Control your Philips Hue lights with AI. The easiest way to set up MCP-based light control on Windows with Claude, VS Code, and more.",
        url: canonicalUrl("/mcp-setup"),
        images: [{
            url: "/screenshots/settings-mcp.png",
            width: 1920,
            height: 1080,
            alt: "Hue Companion MCP Server settings page with setup configuration",
        }],
    },
    twitter: {
        ...sharedTwitter,
        title: `MCP Server Setup — ${SITE_NAME}`,
        description: "Control your Philips Hue lights with AI. The easiest way to set up MCP-based light control on Windows.",
        images: ["/screenshots/settings-mcp.png"],
    },
    alternates: {
        canonical: canonicalUrl("/mcp-setup"),
    },
};

const howToJsonLd = {
    "@context": "https://schema.org",
    "@type": "HowTo",
    name: "How to set up the Hue Companion MCP server for AI light control",
    description: "Connect your Philips Hue lights to AI assistants like Claude Desktop, Claude Code, and VS Code Copilot using Hue Companion's built-in MCP server. The easiest way to control your Hue lights via MCP on Windows.",
    totalTime: "PT5M",
    tool: [
        { "@type": "HowToTool", name: "Hue Companion for Windows" },
        { "@type": "HowToTool", name: "An MCP-compatible AI client (Claude Desktop, Claude Code, or VS Code Copilot)" },
    ],
    supply: [
        { "@type": "HowToSupply", name: "Philips Hue Bridge connected to your local network" },
        { "@type": "HowToSupply", name: "Philips Hue lights paired to the bridge" },
    ],
    step: [
        {
            "@type": "HowToStep",
            name: "Install Hue Companion",
            text: "Download and install Hue Companion for Windows from the Microsoft Store or GitHub releases. Connect it to your Hue Bridge by pressing the link button when prompted.",
            position: 1,
        },
        {
            "@type": "HowToStep",
            name: "Enable the MCP Server",
            text: "Open Hue Companion, go to Settings, and toggle on the MCP Server. The server will start on localhost:5680.",
            position: 2,
        },
        {
            "@type": "HowToStep",
            name: "Configure your AI client",
            text: "Copy the configuration JSON from the Hue Companion Settings page and paste it into your AI client's MCP configuration file. For Claude Desktop, add it to claude_desktop_config.json. For Claude Code, add it to your MCP settings. For VS Code, add it to your Copilot MCP settings.",
            position: 3,
        },
        {
            "@type": "HowToStep",
            name: "Restart and start controlling",
            text: "Restart your AI client to load the new MCP server. You can now control your lights with natural language commands like 'set the living room to warm white' or 'create a sunset animation'.",
            position: 4,
        },
    ],
};

export default function McpSetupPage() {
    return (
        <main className="min-h-screen bg-black text-zinc-200 font-sans selection:bg-purple-900/50">
            <script
                type="application/ld+json"
                dangerouslySetInnerHTML={{ __html: JSON.stringify(howToJsonLd) }}
            />
            <script
                type="application/ld+json"
                dangerouslySetInnerHTML={{
                    __html: JSON.stringify(
                        breadcrumbJsonLd([
                            { name: "Home", url: SITE_URL },
                            { name: "MCP Server Setup", url: canonicalUrl("/mcp-setup") },
                        ])
                    ),
                }}
            />
            <McpSetupContent />
            <Footer />
        </main>
    );
}
