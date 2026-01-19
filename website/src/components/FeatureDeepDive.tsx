"use client";

import { motion } from "framer-motion";
import { cn } from "@/lib/utils";

interface DeepDiveProps {
    title: string;
    description: string;
    imageSrc: string;
    imageAlt: string;
    align?: "left" | "right";
    index: number;
}

export function FeatureDeepDive({ title, description, imageSrc, imageAlt, align = "left", index }: DeepDiveProps) {
    return (
        <section className="py-24 overflow-hidden">
            <div className="container px-4 md:px-6">
                <div className={cn(
                    "flex flex-col gap-12 items-center",
                    align === "left" ? "md:flex-row" : "md:flex-row-reverse"
                )}>

                    <motion.div
                        initial={{ opacity: 0, x: align === "left" ? -50 : 50 }}
                        whileInView={{ opacity: 1, x: 0 }}
                        viewport={{ once: true, margin: "-100px" }}
                        transition={{ duration: 0.8 }}
                        className="flex-1"
                    >
                        <div className="inline-block px-3 py-1 rounded-full bg-white/5 border border-white/10 text-sm text-primary mb-6">
                            Feature 0{index + 1}
                        </div>
                        <h3 className="text-3xl md:text-5xl font-bold mb-6 leading-tight">{title}</h3>
                        <p className="text-lg text-zinc-400 leading-relaxed mb-8">
                            {description}
                        </p>

                        <ul className="space-y-4">
                            {[1, 2, 3].map((i) => (
                                <li key={i} className="flex items-center text-zinc-300">
                                    <div className="w-1.5 h-1.5 rounded-full bg-primary mr-3" />
                                    <span>Detailed point explaining {title.toLowerCase()} benefit {i}</span>
                                </li>
                            ))}
                        </ul>
                    </motion.div>

                    <motion.div
                        initial={{ opacity: 0, x: align === "left" ? 50 : -50 }}
                        whileInView={{ opacity: 1, x: 0 }}
                        viewport={{ once: true, margin: "-100px" }}
                        transition={{ duration: 0.8, delay: 0.2 }}
                        className="flex-1 w-full"
                    >
                        <div className="relative rounded-2xl overflow-hidden glass-panel p-2 aspect-video group">
                            <div className="absolute inset-0 bg-gradient-to-tr from-primary/10 to-transparent opacity-0 group-hover:opacity-100 transition-opacity" />
                            <img
                                src={imageSrc}
                                alt={imageAlt}
                                className="w-full h-full object-cover rounded-xl shadow-2xl"
                            />
                        </div>
                    </motion.div>

                </div>
            </div>
        </section>
    );
}
