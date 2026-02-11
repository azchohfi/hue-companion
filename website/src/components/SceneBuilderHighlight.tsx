"use client";

import { motion } from "framer-motion";
import { Sliders } from "lucide-react";

export function SceneBuilderHighlight() {
    return (
        <section className="py-24 bg-zinc-950 relative overflow-hidden">
            <div className="container mx-auto px-4 md:px-6 relative z-10">
                <div className="grid lg:grid-cols-2 gap-12 items-center">

                    <motion.div
                        initial={{ opacity: 0, x: -20 }}
                        whileInView={{ opacity: 1, x: 0 }}
                        transition={{ duration: 0.6 }}
                        className="order-2 lg:order-1 relative"
                    >
                        <div className="absolute -inset-4 bg-gradient-to-r from-primary to-purple-600 rounded-2xl blur-2xl opacity-20" />
                        <img
                            src="/screenshots/scene-builder-keyframes.png"
                            alt="Scene Builder with keyframes across multiple light tracks"
                            className="relative rounded-xl border border-white/10 shadow-2xl w-full"
                        />
                    </motion.div>

                    <div className="order-1 lg:order-2">
                        <div className="inline-flex items-center rounded-full border border-primary/30 bg-primary/10 px-3 py-1 text-sm text-primary mb-6">
                            <Sliders className="w-4 h-4 mr-2" />
                            Scene Builder
                        </div>

                        <h2 className="text-4xl md:text-5xl font-bold mb-6">
                            Built-in <br />
                            <span className="text-gradient">Animation Studio.</span>
                        </h2>

                        <p className="text-lg text-zinc-400 leading-relaxed mb-6">
                            Create timeline-based lighting animations with keyframes, multiple tracks, and event triggers.
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
