"use client";

import { motion } from "framer-motion";
import { Sparkles, Wand2 } from "lucide-react";
import { useState } from "react";

export function LuminaAI() {
    const [active, setActive] = useState(false);

    return (
        <section className="py-32 relative overflow-hidden">
            <div className="container px-4 md:px-6 relative z-10 flex flex-col items-center text-center">

                <motion.div
                    initial={{ opacity: 0, y: 20 }}
                    whileInView={{ opacity: 1, y: 0 }}
                    className="inline-flex items-center rounded-full border border-purple-500/30 bg-purple-500/10 px-4 py-1.5 text-sm text-purple-300 mb-8"
                >
                    <Sparkles className="w-4 h-4 mr-2" />
                    <span>New in 2.0</span>
                </motion.div>

                <motion.h2
                    initial={{ opacity: 0, y: 20 }}
                    whileInView={{ opacity: 1, y: 0 }}
                    className="text-5xl md:text-7xl font-bold mb-6"
                >
                    Lumina AI
                </motion.h2>

                <motion.p
                    initial={{ opacity: 0, y: 20 }}
                    whileInView={{ opacity: 1, y: 0 }}
                    transition={{ delay: 0.1 }}
                    className="text-xl text-zinc-400 max-w-2xl mx-auto mb-12"
                >
                    Type a mood, a movie, or a memory. <br />
                    Lumina generates a complex, multi-track lighting animation instantly.
                </motion.p>

                {/* Interactive Demo */}
                <div className="w-full max-w-xl relative group">
                    <div className={`absolute inset-0 bg-gradient-to-r from-blue-600 via-purple-600 to-pink-600 rounded-2xl blur-xl transition-opacity duration-1000 ${active ? "opacity-40" : "opacity-0"}`} />

                    <div className="relative bg-zinc-900 border border-white/10 rounded-2xl p-2 flex items-center shadow-2xl">
                        <div className="h-10 w-10 rounded-xl bg-zinc-800 flex items-center justify-center mr-4">
                            <Wand2 className="w-5 h-5 text-zinc-400" />
                        </div>
                        <input
                            type="text"
                            placeholder="Cyberpunk noodle shop in the rain..."
                            className="bg-transparent border-none outline-none flex-1 text-white placeholder:text-zinc-600 h-10"
                            onFocus={() => setActive(true)}
                            onBlur={() => setActive(false)}
                        />
                        <button className="bg-white text-black px-4 py-2 rounded-xl font-medium text-sm hover:bg-zinc-200 transition-colors">
                            Generate
                        </button>
                    </div>

                    {/* Generated Palette Visualization */}
                    <motion.div
                        className="mt-8 grid grid-cols-5 gap-2 h-16 opacity-0 group-hover:opacity-100 transition-opacity"
                    >
                        {["bg-cyan-500", "bg-blue-600", "bg-purple-600", "bg-fuchsia-500", "bg-pink-500"].map((color, i) => (
                            <motion.div
                                key={i}
                                initial={{ scale: 0.8, opacity: 0 }}
                                whileInView={{ scale: 1, opacity: 1 }}
                                transition={{ delay: i * 0.1 }}
                                className={`${color} rounded-lg shadow-lg`}
                            />
                        ))}
                    </motion.div>
                </div>

            </div>
        </section>
    );
}
