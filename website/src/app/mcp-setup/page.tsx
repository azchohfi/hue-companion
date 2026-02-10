import { Footer } from "@/components/Footer";
import { McpSetupContent } from "@/components/McpSetupContent";
import { Metadata } from "next";

export const metadata: Metadata = {
    title: "MCP Server Setup - Hue Companion",
    description:
        "Set up the Hue Companion MCP server to control your Philips Hue lights from Claude, VS Code, and other AI assistants.",
    alternates: {
        canonical: "https://hue-companion.drayne.xyz/mcp-setup",
    },
};

export default function McpSetupPage() {
    return (
        <main className="min-h-screen bg-black text-zinc-200 font-sans selection:bg-purple-900/50">
            <McpSetupContent />
            <Footer />
        </main>
    );
}
