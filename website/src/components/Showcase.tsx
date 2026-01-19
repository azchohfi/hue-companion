"use client";

import { motion } from "framer-motion";
import { useEffect, useState } from "react";

const screenshots = [
    "/screenshots/shot1.png",
    "/screenshots/shot2.png",
    "/screenshots/shot3.png",
    "/screenshots/shot4.png",
];

export function Showcase() {
    const [offset, setOffset] = useState(0);

    // Auto-scroll logic could go here, or just use CSS animation

    return (
        <section className="py-24 bg-black overflow-hidden relative">
            <div className="container px-4 md:px-6 mb-12 relative z-10">
                <h2 className="text-3xl md:text-5xl font-bold text-center mb-6">Designed for Windows 11.</h2>
                <p className="text-zinc-400 text-center max-w-2xl mx-auto mb-12">
                    Every pixel is crafted to look and feel native, while providing features the official app lacks.
                </p>
            </div>

            <div className="flex gap-8 animate-scroll whitespace-nowrap px-4 w-max">
                {/* Double the array for infinite scroll effect */}
                {[...screenshots, ...screenshots].map((src, i) => (
                    <div key={i} className="inline-block w-[600px] rounded-xl overflow-hidden shadow-2xl border border-white/10 relative group">
                        <div className="absolute inset-0 bg-black/0 group-hover:bg-black/10 transition-colors duration-300" />
                        <img src={src} alt="App Screenshot" className="w-full h-auto object-cover" />
                    </div>
                ))}
            </div>

            <style jsx>{`
        @keyframes scroll {
          0% { transform: translateX(0); }
          100% { transform: translateX(calc(-100% / 2 - 2rem)); }
        }
        .animate-scroll {
          animation: scroll 40s linear infinite;
        }
        .animate-scroll:hover {
            animation-play-state: paused;
        }
      `}</style>
        </section>
    );
}
