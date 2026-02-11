"use client";

import { motion } from "framer-motion";
import { Bot, Lightbulb, Palette, PlayCircle } from "lucide-react";
import Link from "next/link";

const capabilities = [
    { icon: Lightbulb, label: "Control lights by name — brightness, color, on/off" },
    { icon: Palette, label: "Create and activate scenes with natural language" },
    { icon: PlayCircle, label: "Build and play animated lighting scenes" },
];

export function McpShowcase() {
    return (
        <section className="py-32 relative overflow-hidden">
            <div className="container px-4 md:px-6 relative z-10 flex flex-col items-center text-center">

                <motion.div
                    initial={{ opacity: 0, y: 20 }}
                    whileInView={{ opacity: 1, y: 0 }}
                    className="inline-flex items-center rounded-full border border-purple-500/30 bg-purple-500/10 px-4 py-1.5 text-sm text-purple-300 mb-8"
                >
                    <Bot className="w-4 h-4 mr-2" />
                    <span>New</span>
                </motion.div>

                <motion.h2
                    initial={{ opacity: 0, y: 20 }}
                    whileInView={{ opacity: 1, y: 0 }}
                    className="text-5xl md:text-7xl font-bold mb-6"
                >
                    AI-Powered Control
                </motion.h2>

                <motion.p
                    initial={{ opacity: 0, y: 20 }}
                    whileInView={{ opacity: 1, y: 0 }}
                    transition={{ delay: 0.1 }}
                    className="text-xl text-zinc-400 max-w-2xl mx-auto mb-12"
                >
                    Connect Hue Companion to Claude, VS Code, and other AI assistants
                    via the Model Context Protocol.
                </motion.p>

                {/* Screenshot + Capabilities */}
                <div className="w-full max-w-4xl grid md:grid-cols-2 gap-8 items-center text-left">
                    <motion.div
                        initial={{ opacity: 0, x: -20 }}
                        whileInView={{ opacity: 1, x: 0 }}
                        transition={{ delay: 0.2 }}
                        className="relative"
                    >
                        <div className="absolute -inset-4 bg-gradient-to-r from-purple-600 to-blue-600 rounded-2xl blur-2xl opacity-20" />
                        <img
                            src="/screenshots/claude-mcp.png"
                            alt="Claude AI controlling Hue lights via MCP"
                            className="relative rounded-xl border border-white/10 shadow-2xl w-full"
                        />
                    </motion.div>

                    <motion.div
                        initial={{ opacity: 0, x: 20 }}
                        whileInView={{ opacity: 1, x: 0 }}
                        transition={{ delay: 0.3 }}
                    >
                        <p className="text-zinc-400 mb-6">
                            Tell your AI assistant to control your lights using plain English.
                            Hue Companion&apos;s built-in MCP server handles the rest.
                        </p>

                        <ul className="space-y-4 mb-8">
                            {capabilities.map((cap, i) => (
                                <li key={i} className="flex items-center gap-3">
                                    <div className="w-8 h-8 rounded-lg bg-purple-500/10 border border-purple-500/20 flex items-center justify-center flex-shrink-0">
                                        <cap.icon className="w-4 h-4 text-purple-400" />
                                    </div>
                                    <span className="text-zinc-300 text-sm">{cap.label}</span>
                                </li>
                            ))}
                        </ul>

                        <Link
                            href="/mcp-setup"
                            className="inline-flex h-10 items-center justify-center rounded-full border border-purple-500/30 bg-purple-500/10 px-6 font-medium text-purple-300 text-sm transition hover:bg-purple-500/20"
                        >
                            Setup Guide
                        </Link>
                    </motion.div>
                </div>
            </div>
        </section>
    );
}
