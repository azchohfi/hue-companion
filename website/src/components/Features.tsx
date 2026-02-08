"use client";

import { motion } from "framer-motion";
import { Layers, Zap, Monitor, Palette, Flame, Grid3X3, Keyboard, MonitorDown } from "lucide-react";

const features = [
    {
        icon: <Monitor className="w-6 h-6" />,
        title: "Native Performance",
        description: "Built with WinUI 3 for fast startup and low memory usage.",
    },
    {
        icon: <Layers className="w-6 h-6" />,
        title: "Rooms, Zones & Lights",
        description: "Control individual lights, entire rooms, or custom zones. Brightness, color, and on/off for everything.",
    },
    {
        icon: <Flame className="w-6 h-6" />,
        title: "Native Hue Effects",
        description: "Fire, candle, sparkle, prism, underwater, cosmos — all built-in Hue effects with adjustable speed and brightness.",
    },
    {
        icon: <Palette className="w-6 h-6" />,
        title: "Scene Builder",
        description: "DAW-style timeline editor for creating complex, multi-track animated lighting scenes with keyframes and events.",
    },
    {
        icon: <Grid3X3 className="w-6 h-6" />,
        title: "Custom Dashboard",
        description: "Pin your favorite rooms, zones, and individual lights to a custom dashboard for quick one-tap access.",
    },
    {
        icon: <Keyboard className="w-6 h-6" />,
        title: "Global Hotkeys",
        description: "Control your lights from anywhere with system-wide keyboard shortcuts, even when the app is minimized.",
    },
    {
        icon: <MonitorDown className="w-6 h-6" />,
        title: "System Tray",
        description: "Minimize to the system tray and launch at startup. Always one click away without cluttering your taskbar.",
    },
    {
        icon: <Zap className="w-6 h-6" />,
        title: "Multi-Bridge",
        description: "Connect multiple Hue Bridges simultaneously. All your rooms and zones from every bridge in one app.",
    },
];

export function Features() {
    return (
        <section className="py-24 bg-zinc-950 relative overflow-hidden">
            <div className="container px-4 md:px-6 relative z-10">
                <div className="text-center mb-16">
                    <h2 className="text-3xl md:text-5xl font-bold mb-4">Features</h2>
                    <p className="text-zinc-400 max-w-2xl mx-auto">
                        What you can do with Hue Companion.
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
