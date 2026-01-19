"use client";

import { motion } from "framer-motion";

export function CreativeHotkeys() {
    const keys = ["CTRL", "ALT", "L"];

    return (
        <section className="py-32 relative flex flex-col items-center justify-center min-h-[60vh]">
            <div className="text-center mb-16 relative z-10">
                <h2 className="text-4xl md:text-6xl font-bold mb-4">Instant Control</h2>
                <p className="text-zinc-400">Map any scene to any key. Your lights, your layout.</p>
            </div>

            <div className="flex gap-4 md:gap-8 perspective-1000">
                {keys.map((key, i) => (
                    <motion.div
                        key={key}
                        initial={{ opacity: 0, y: 50, rotateX: 45 }}
                        whileInView={{ opacity: 1, y: 0, rotateX: 0 }}
                        whileHover={{ y: -10, color: "#000" }}
                        transition={{ delay: i * 0.1, type: "spring" }}
                        className="w-24 h-24 md:w-32 md:h-32 bg-zinc-900 border border-white/10 rounded-2xl flex items-center justify-center text-2xl md:text-4xl font-bold shadow-2xl relative overflow-hidden group hover:bg-white hover:text-black transition-colors duration-300"
                    >
                        <div className="absolute inset-x-0 bottom-0 h-1 bg-gradient-to-r from-transparent via-primary to-transparent opacity-50 group-hover:opacity-100" />
                        {key}

                        {/* 3D Depth Mockup */}
                        <div className="absolute -bottom-2 -right-2 w-full h-full border-r-4 border-b-4 border-zinc-800 rounded-2xl -z-10 group-hover:translate-x-1 group-hover:translate-y-1 transition-transform" />

                        <div className="absolute top-2 right-2 text-[10px] text-zinc-500 font-mono opacity-0 group-hover:opacity-100 transition-opacity">
                            EDIT
                        </div>
                    </motion.div>
                ))}
            </div>
        </section>
    );
}
