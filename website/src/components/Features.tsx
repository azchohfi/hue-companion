"use client";

import { motion } from "framer-motion";
import { Layers, Zap, Monitor, Palette } from "lucide-react";

const features = [
    {
        icon: <Monitor className="w-6 h-6" />,
        title: "Native Performance",
        description: "Built with Windows App SDK for blazing fast localized control. No Electron lag, just pure performance.",
    },
    {
        icon: <Layers className="w-6 h-6" />,
        title: "Zone & Room Management",
        description: "Control individual lights, entire rooms, or custom zones with ease. Sync your entire house in one click.",
    },
    {
        icon: <Zap className="w-6 h-6" />,
        title: "Live Preview",
        description: "Hover over scenes or colors to instantly preview them on your lights before applying.",
    },
    {
        icon: <Palette className="w-6 h-6" />,
        title: "Dynamic Scenes",
        description: "Create and save complex gradient scenes. Import from Hue gallery or design your own.",
    },
];

export function Features() {
    return (
        <section className="py-24 bg-zinc-950 relative overflow-hidden">
            <div className="container px-4 md:px-6 relative z-10">
                <div className="text-center mb-16">
                    <h2 className="text-3xl md:text-5xl font-bold mb-4">Everything you need.</h2>
                    <p className="text-zinc-400 max-w-2xl mx-auto">
                        Designed for power users who want more control over their lighting setup.
                    </p>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-8">
                    {features.map((feature, index) => (
                        <motion.div
                            key={index}
                            initial={{ opacity: 0, y: 20 }}
                            whileInView={{ opacity: 1, y: 0 }}
                            viewport={{ once: true }}
                            transition={{ delay: index * 0.1, duration: 0.5 }}
                            className="p-6 rounded-2xl bg-white/5 border border-white/10 hover:bg-white/10 transition-colors backdrop-blur-sm"
                        >
                            <div className="w-12 h-12 rounded-full bg-primary/20 flex items-center justify-center text-primary mb-4">
                                {feature.icon}
                            </div>
                            <h3 className="text-xl font-semibold mb-2">{feature.title}</h3>
                            <p className="text-zinc-400 text-sm leading-relaxed">
                                {feature.description}
                            </p>
                        </motion.div>
                    ))}
                </div>
            </div>
        </section>
    );
}
