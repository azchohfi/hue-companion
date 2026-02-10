"use client";

import { motion } from "framer-motion";

export function CreativeNative() {
    return (
        <section className="py-32 relative overflow-hidden flex flex-col items-center">
            <div className="container px-4 md:px-6 relative z-10 text-center mb-16">
                <h2 className="text-4xl md:text-6xl font-bold mb-4">Native to Windows.</h2>
                <p className="text-zinc-400 max-w-2xl mx-auto">
                    Built with WinUI 3 — the same framework as Windows 11 system apps.
                </p>
            </div>

            <div className="relative w-full max-w-4xl perspective-1000 px-4">
                <motion.div
                    initial={{ rotateX: 10, opacity: 0 }}
                    whileInView={{ rotateX: 0, opacity: 1 }}
                    transition={{ duration: 1.2, type: "spring" }}
                    className="relative"
                >
                    <img
                        src="/screenshots/room-lights-dark.png"
                        alt="Hue Companion room detail showing native WinUI 3 controls"
                        className="rounded-xl border border-white/10 shadow-2xl w-full"
                    />

                    {/* Reflection Glare */}
                    <div className="absolute inset-0 bg-gradient-to-tr from-white/5 to-transparent pointer-events-none rounded-xl" />
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
