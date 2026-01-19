"use client";

import { motion } from "framer-motion";
import { Play, Settings2, Sliders } from "lucide-react";

export function SceneBuilderHighlight() {
    return (
        <section className="py-24 bg-zinc-950 relative overflow-hidden">
            <div className="container px-4 md:px-6 relative z-10">
                <div className="grid lg:grid-cols-2 gap-12 items-center">

                    <div className="order-2 lg:order-1 relative">
                        <div className="absolute -inset-4 bg-gradient-to-r from-primary to-purple-600 rounded-2xl blur-2xl opacity-20 animate-pulse" />
                        <div className="relative rounded-xl overflow-hidden border border-white/10 shadow-2xl bg-zinc-900/50 backdrop-blur-xl p-2">
                            {/* Simulated UI for Scene Builder */}
                            <div className="aspect-video bg-black/60 rounded-lg flex items-center justify-center relative overflow-hidden group">
                                <div className="absolute inset-x-0 top-0 h-16 border-b border-white/5 bg-zinc-900/80 flex items-center px-4 gap-2">
                                    <div className="w-3 h-3 rounded-full bg-red-500/50" />
                                    <div className="w-3 h-3 rounded-full bg-yellow-500/50" />
                                    <div className="w-3 h-3 rounded-full bg-green-500/50" />
                                    <div className="ml-4 h-6 w-32 bg-white/10 rounded-md" />
                                </div>

                                {/* Animated Waveform Representation */}
                                <div className="flex gap-1 h-32 items-end opacity-50">
                                    {[...Array(20)].map((_, i) => (
                                        <motion.div
                                            key={i}
                                            animate={{ height: ["20%", "80%", "20%"] }}
                                            transition={{
                                                duration: 1.5,
                                                repeat: Infinity,
                                                delay: i * 0.1,
                                                ease: "easeInOut"
                                            }}
                                            className="w-4 bg-gradient-to-t from-primary to-transparent rounded-t-sm"
                                        />
                                    ))}
                                </div>

                                <motion.div
                                    whileHover={{ scale: 1.1 }}
                                    className="absolute inset-0 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity"
                                >
                                    <div className="w-16 h-16 rounded-full bg-white/10 backdrop-blur-md flex items-center justify-center border border-white/20">
                                        <Play className="fill-white text-white ml-1" />
                                    </div>
                                </motion.div>
                            </div>

                            {/* Timeline controls */}
                            <div className="mt-2 h-12 bg-zinc-900/80 rounded-lg flex items-center px-4 gap-4">
                                <Play className="w-4 h-4 text-zinc-400" />
                                <div className="h-1 flex-1 bg-zinc-800 rounded-full overflow-hidden">
                                    <motion.div
                                        animate={{ width: ["0%", "100%"] }}
                                        transition={{ duration: 10, repeat: Infinity, ease: "linear" }}
                                        className="h-full bg-primary"
                                    />
                                </div>
                                <Settings2 className="w-4 h-4 text-zinc-400" />
                            </div>
                        </div>
                    </div>

                    <div className="order-1 lg:order-2">
                        <div className="inline-flex items-center rounded-full border border-primary/30 bg-primary/10 px-3 py-1 text-sm text-primary mb-6">
                            <Sliders className="w-4 h-4 mr-2" />
                            Pro Feature
                        </div>

                        <h2 className="text-4xl md:text-5xl font-bold mb-6">
                            Pro-Grade <br />
                            <span className="text-gradient">Animation Studio.</span>
                        </h2>

                        <p className="text-lg text-zinc-400 leading-relaxed mb-6">
                            Don't settle for static lighting. Our built-in Scene Builder lets you craft complex, timeline-based animations for your lights.
                        </p>

                        <ul className="space-y-4 mb-8">
                            <li className="flex items-start">
                                <div className="mt-1 w-5 h-5 rounded-full bg-primary/20 flex items-center justify-center text-primary text-xs mr-3">1</div>
                                <div>
                                    <strong className="text-white block">Keyframe Control</strong>
                                    <span className="text-zinc-500">Define exact colors and brightness at any point in time.</span>
                                </div>
                            </li>
                            <li className="flex items-start">
                                <div className="mt-1 w-5 h-5 rounded-full bg-primary/20 flex items-center justify-center text-primary text-xs mr-3">2</div>
                                <div>
                                    <strong className="text-white block">Multi-Track Editing</strong>
                                    <span className="text-zinc-500">Layer different effects for different zones simultaneously.</span>
                                </div>
                            </li>
                            <li className="flex items-start">
                                <div className="mt-1 w-5 h-5 rounded-full bg-primary/20 flex items-center justify-center text-primary text-xs mr-3">3</div>
                                <div>
                                    <strong className="text-white block">Live Playback</strong>
                                    <span className="text-zinc-500">See your creation on your real lights as you edit.</span>
                                </div>
                            </li>
                        </ul>
                    </div>

                </div>
            </div>
        </section>
    );
}
