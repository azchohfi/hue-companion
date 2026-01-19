"use client";

import { motion } from "framer-motion";

export function CreativeNative() {
    return (
        <section className="py-32 relative overflow-hidden flex flex-col items-center">
            <div className="container px-4 md:px-6 relative z-10 text-center mb-16">
                <h2 className="text-4xl md:text-6xl font-bold mb-4">Unapologetically Native.</h2>
                <p className="text-zinc-400 max-w-2xl mx-auto">
                    It doesn't just look like a Windows app. It feels like one.
                </p>
            </div>

            <div className="relative w-full max-w-4xl perspective-1000 px-4">
                <motion.div
                    initial={{ rotateX: 20, rotateY: -20, opacity: 0 }}
                    whileInView={{ rotateX: 0, rotateY: 0, opacity: 1 }}
                    transition={{ duration: 1.2, type: "spring" }}
                    className="relative aspect-video bg-zinc-900/80 border border-white/10 rounded-xl shadow-2xl backdrop-blur-xl overflow-hidden"
                >
                    {/* Fake Window Controls */}
                    <div className="h-8 bg-zinc-900/50 border-b border-white/5 flex items-center justify-between px-3">
                        <span className="text-xs text-zinc-500">Hue Companion</span>
                        <div className="flex gap-2">
                            <div className="w-3 h-3 rounded-full bg-zinc-700" />
                            <div className="w-3 h-3 rounded-full bg-zinc-700" />
                            <div className="w-3 h-3 rounded-full bg-zinc-700" />
                        </div>
                    </div>

                    {/* Content Mockup */}
                    <div className="p-6 grid grid-cols-2 gap-6 h-full">
                        <div className="space-y-4">
                            <div className="h-20 bg-primary/20 rounded-lg animate-pulse" />
                            <div className="h-20 bg-white/5 rounded-lg" />
                            <div className="h-20 bg-white/5 rounded-lg" />
                        </div>
                        <div className="bg-zinc-950/50 rounded-lg p-4 relative overflow-hidden">
                            <div className="absolute inset-0 bg-gradient-to-tr from-primary/10 to-transparent" />
                        </div>
                    </div>

                    {/* Reflection Glare */}
                    <div className="absolute inset-0 bg-gradient-to-tr from-white/5 to-transparent pointer-events-none" />
                </motion.div>

                {/* Floating Elements */}
                <motion.div
                    animate={{ y: [0, -20, 0] }}
                    transition={{ duration: 4, repeat: Infinity, ease: "easeInOut" }}
                    className="absolute -right-8 top-1/4 bg-zinc-800/90 backdrop-blur border border-white/10 p-4 rounded-xl shadow-xl z-20"
                >
                    <div className="text-xs font-bold text-zinc-400 mb-2">MEMORY USAGE</div>
                    <div className="text-2xl font-mono text-green-400">42 MB</div>
                </motion.div>

                <motion.div
                    animate={{ y: [0, 20, 0] }}
                    transition={{ duration: 5, repeat: Infinity, ease: "easeInOut", delay: 1 }}
                    className="absolute -left-8 bottom-1/4 bg-zinc-800/90 backdrop-blur border border-white/10 p-4 rounded-xl shadow-xl z-20"
                >
                    <div className="text-xs font-bold text-zinc-400 mb-2">STARTUP TIME</div>
                    <div className="text-2xl font-mono text-blue-400">0.3s</div>
                </motion.div>
            </div>
        </section>
    );
}
