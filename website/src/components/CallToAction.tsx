"use client";

import { motion } from "framer-motion";
import { ArrowRight, Download } from "lucide-react";

export function CallToAction() {
    return (
        <section className="py-32 relative overflow-hidden">
            <div className="absolute inset-0 bg-gradient-to-b from-transparent to-primary/5 pointer-events-none" />

            <div className="container px-4 md:px-6 relative z-10 text-center">
                <motion.h2
                    initial={{ opacity: 0, y: 20 }}
                    whileInView={{ opacity: 1, y: 0 }}
                    viewport={{ once: true }}
                    className="text-4xl md:text-6xl font-bold mb-8"
                >
                    Ready to take control?
                </motion.h2>
                <motion.p
                    initial={{ opacity: 0, y: 20 }}
                    whileInView={{ opacity: 1, y: 0 }}
                    viewport={{ once: true }}
                    transition={{ delay: 0.1 }}
                    className="text-xl text-zinc-400 max-w-2xl mx-auto mb-10"
                >
                    Join thousands of users who have upgraded their lighting experience.
                </motion.p>

                <motion.div
                    initial={{ opacity: 0, y: 20 }}
                    whileInView={{ opacity: 1, y: 0 }}
                    viewport={{ once: true }}
                    transition={{ delay: 0.2 }}
                    className="flex flex-col sm:flex-row gap-4 justify-center"
                >
                    <button className="inline-flex h-14 items-center justify-center rounded-full bg-primary text-white px-10 font-bold text-lg transition hover:bg-primary/90 shadow-lg shadow-primary/25">
                        <Download className="mr-2 h-5 w-5" />
                        Download for Windows
                    </button>
                </motion.div>
            </div>
        </section>
    );
}
