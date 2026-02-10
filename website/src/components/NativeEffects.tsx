"use client";

import { motion } from "framer-motion";
import { Flame } from "lucide-react";

const effects = [
    { name: "Fire", color: "from-orange-600 to-red-700", delay: 0 },
    { name: "Candle", color: "from-amber-500 to-orange-600", delay: 0.8 },
    { name: "Sparkle", color: "from-yellow-300 to-amber-500", delay: 0.3 },
    { name: "Glisten", color: "from-slate-300 to-zinc-400", delay: 0.9 },
    { name: "Opal", color: "from-rose-300 to-sky-300", delay: 0.1 },
    { name: "Prism", color: "from-violet-500 to-pink-500", delay: 0.5 },
    { name: "Underwater", color: "from-cyan-500 to-blue-700", delay: 0.2 },
    { name: "Cosmos", color: "from-indigo-600 to-purple-800", delay: 0.7 },
    { name: "Sunbeam", color: "from-yellow-400 to-orange-500", delay: 0.4 },
    { name: "Enchant", color: "from-fuchsia-500 to-violet-600", delay: 0.6 },
];

export function NativeEffects() {
    return (
        <section className="py-32 relative overflow-hidden">
            <div className="container px-4 md:px-6 relative z-10 flex flex-col items-center text-center">

                <motion.div
                    initial={{ opacity: 0, y: 20 }}
                    whileInView={{ opacity: 1, y: 0 }}
                    className="inline-flex items-center rounded-full border border-orange-500/30 bg-orange-500/10 px-4 py-1.5 text-sm text-orange-300 mb-8"
                >
                    <Flame className="w-4 h-4 mr-2" />
                    <span>Built-in Effects</span>
                </motion.div>

                <motion.h2
                    initial={{ opacity: 0, y: 20 }}
                    whileInView={{ opacity: 1, y: 0 }}
                    className="text-5xl md:text-7xl font-bold mb-6"
                >
                    Native Hue Effects
                </motion.h2>

                <motion.p
                    initial={{ opacity: 0, y: 20 }}
                    whileInView={{ opacity: 1, y: 0 }}
                    transition={{ delay: 0.1 }}
                    className="text-xl text-zinc-400 max-w-2xl mx-auto mb-12"
                >
                    10 built-in effects powered by Hue&apos;s native engine. <br />
                    Adjust speed and brightness, then apply to any light or save as a scene.
                </motion.p>

                {/* Effects Grid */}
                <div className="w-full max-w-3xl grid grid-cols-2 md:grid-cols-5 gap-3">
                    {effects.map((effect, i) => (
                        <motion.div
                            key={effect.name}
                            initial={{ opacity: 0, scale: 0.9 }}
                            whileInView={{ opacity: 1, scale: 1 }}
                            transition={{ delay: i * 0.05 }}
                            whileHover={{ scale: 1.05, y: -4 }}
                            className="relative group cursor-default"
                        >
                            <div className={`absolute inset-0 bg-gradient-to-br ${effect.color} rounded-xl blur-xl opacity-0 group-hover:opacity-30 transition-opacity duration-500`} />
                            <div className="relative bg-zinc-900 border border-white/10 rounded-xl p-5 flex flex-col items-center gap-3 group-hover:border-white/20 transition-colors">
                                <motion.div
                                    animate={{ opacity: [0.5, 1, 0.5] }}
                                    transition={{ duration: 2, repeat: Infinity, delay: effect.delay }}
                                    className={`w-8 h-8 rounded-full bg-gradient-to-br ${effect.color}`}
                                />
                                <span className="text-sm font-medium text-zinc-300">{effect.name}</span>
                            </div>
                        </motion.div>
                    ))}
                </div>

            </div>
        </section>
    );
}
