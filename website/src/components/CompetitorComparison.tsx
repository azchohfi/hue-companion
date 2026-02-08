"use client";

import { motion } from "framer-motion";
import { Check, X, Minus } from "lucide-react";
import { cn } from "@/lib/utils";

const features = [
    { name: "Windows Native (WinUI 3)", hueWindows: true, others: false },
    { name: "Global Hotkeys", hueWindows: true, others: false },
    { name: "Multi-Bridge Support", hueWindows: true, others: false },
    { name: "Native Hue Effects", hueWindows: true, others: "limited" },
    { name: "Scene Builder", hueWindows: true, others: false },
    { name: "Custom Dashboard", hueWindows: true, others: false },
    { name: "RAM Usage", hueWindows: "~40MB", others: "400MB+" },
    { name: "Startup Time", hueWindows: "0.3s", others: "5s+" },
];

export function CompetitorComparison() {
    return (
        <section className="py-24 container px-4 md:px-6">
            <div className="text-center mb-16">
                <h2 className="text-3xl md:text-5xl font-bold mb-4">Why switch?</h2>
                <p className="text-zinc-400">See how Hue Companion compares to Electron-based alternatives.</p>
            </div>

            <div className="max-w-4xl mx-auto overflow-hidden rounded-3xl border border-white/10 bg-zinc-900/40 backdrop-blur-md shadow-2xl">
                <div className="grid grid-cols-3 p-6 border-b border-white/10 bg-white/5">
                    <div className="col-span-1 font-medium text-zinc-400 flex items-end pb-2">Feature</div>
                    <div className="col-span-1 font-bold text-xl md:text-2xl text-center text-primary flex flex-col items-center">
                        <div className="text-sm font-normal text-zinc-500 mb-2">Hue Companion</div>
                    </div>
                    <div className="col-span-1 font-medium text-zinc-500 text-center flex flex-col items-end justify-end pb-2">
                        <span>Others</span>
                    </div>
                </div>

                {features.map((feature, i) => (
                    <motion.div
                        key={feature.name}
                        initial={{ opacity: 0, y: 10 }}
                        whileInView={{ opacity: 1, y: 0 }}
                        transition={{ delay: i * 0.05 }}
                        className={cn(
                            "grid grid-cols-3 p-6 items-center border-b border-white/5 hover:bg-white/5 transition-colors",
                            i === features.length - 1 && "border-0"
                        )}
                    >
                        <div className="font-medium text-zinc-200">{feature.name}</div>

                        <div className="flex justify-center">
                            {feature.hueWindows === true ? (
                                <div className="h-8 w-8 rounded-full bg-primary/20 flex items-center justify-center text-primary">
                                    <Check className="w-5 h-5" />
                                </div>
                            ) : (
                                <span className="font-mono text-primary font-bold">{feature.hueWindows}</span>
                            )}
                        </div>

                        <div className="flex justify-end pr-4 md:pr-8">
                            {feature.others === false ? (
                                <X className="w-5 h-5 text-zinc-600" />
                            ) : feature.others === "limited" ? (
                                <span className="text-xs uppercase tracking-wider text-zinc-500 font-medium">Limited</span>
                            ) : (
                                <span className="font-mono text-zinc-500">{feature.others}</span>
                            )}
                        </div>
                    </motion.div>
                ))}
            </div>
        </section>
    );
}
