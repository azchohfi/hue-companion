"use client";

import { motion } from "framer-motion";
import { ArrowRight, Download } from "lucide-react";
import Link from "next/link";

export function Hero() {
    return (
        <section className="relative min-h-screen flex items-center justify-center overflow-hidden pt-20">
            {/* Background Gradient Orbs */}
            <div className="absolute inset-0 overflow-hidden pointer-events-none">
                <motion.div
                    animate={{
                        scale: [1, 1.2, 1],
                        opacity: [0.3, 0.5, 0.3],
                    }}
                    transition={{
                        duration: 8,
                        repeat: Infinity,
                        ease: "easeInOut",
                    }}
                    className="absolute -top-1/2 -left-1/2 w-[1000px] h-[1000px] bg-primary/20 rounded-full blur-3xl opacity-30"
                />
                <motion.div
                    animate={{
                        scale: [1, 1.1, 1],
                        opacity: [0.2, 0.4, 0.2],
                    }}
                    transition={{
                        duration: 10,
                        repeat: Infinity,
                        ease: "easeInOut",
                        delay: 1,
                    }}
                    className="absolute top-0 right-0 w-[800px] h-[800px] bg-accent/20 rounded-full blur-3xl opacity-20"
                />
            </div>

            <div className="container mx-auto px-4 md:px-6 relative z-10 flex flex-col items-center text-center">
                <motion.div
                    initial={{ opacity: 0, y: 20 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ duration: 0.8, ease: "easeOut" }}
                    className="inline-flex items-center rounded-full border border-white/10 bg-white/5 px-3 py-1 text-sm text-zinc-400 mb-8 backdrop-blur-md"
                >
                    <span className="flex h-2 w-2 rounded-full bg-green-500 mr-2"></span>
                    Available for Windows 10 & 11
                </motion.div>

                <motion.h1
                    initial={{ opacity: 0, y: 30 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ duration: 0.8, delay: 0.2, ease: "easeOut" }}
                    className="text-5xl md:text-7xl lg:text-8xl font-bold tracking-tight mb-6 max-w-4xl"
                >
                    Control your lights.{" "}
                    <span className="text-gradient">Native to Windows.</span>
                </motion.h1>

                <motion.p
                    initial={{ opacity: 0, y: 30 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ duration: 0.8, delay: 0.4, ease: "easeOut" }}
                    className="text-lg md:text-xl text-zinc-400 max-w-2xl mb-10 leading-relaxed"
                >
                    A Philips Hue client for Windows, built with WinUI 3 for a fast, native experience.
                </motion.p>

                <motion.div
                    initial={{ opacity: 0, y: 30 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ duration: 0.8, delay: 0.6, ease: "easeOut" }}
                    className="flex flex-col sm:flex-row gap-4"
                >
                    <button className="inline-flex h-12 items-center justify-center rounded-full bg-white text-black px-8 font-medium transition active:scale-95 hover:bg-zinc-200">
                        <Download className="mr-2 h-4 w-4" />
                        Get on Store
                    </button>
                    <Link href="/features" className="inline-flex h-12 items-center justify-center rounded-full border border-white/10 bg-white/5 px-8 font-medium text-white transition active:scale-95 hover:bg-white/10 backdrop-blur-sm">
                        View Features
                        <ArrowRight className="ml-2 h-4 w-4" />
                    </Link>
                </motion.div>

                {/* Placeholder for the hero image to peek in */}
                <motion.div
                    initial={{ opacity: 0, y: 100, rotateX: 20 }}
                    animate={{ opacity: 1, y: 0, rotateX: 0 }}
                    transition={{ duration: 1.2, delay: 0.8, ease: "easeOut" }}
                    className="mt-20 w-full max-w-5xl rounded-xl overflow-hidden glass-panel p-2 transform-3d perspective-1000"
                >
                    <img src="/screenshots/home-dark.png" alt="Hue Companion — Home page showing rooms with live light status" className="rounded-lg shadow-2xl w-full" />
                </motion.div>
            </div>
        </section>
    );
}
