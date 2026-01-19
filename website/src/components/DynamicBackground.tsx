"use client";

import { useScroll, useTransform, motion } from "framer-motion";

export function DynamicBackground() {
    const { scrollYProgress } = useScroll();

    const background = useTransform(
        scrollYProgress,
        [0, 0.25, 0.5, 0.75, 1],
        [
            "radial-gradient(circle at 50% 0%, #1a1a1a 0%, #000000 100%)", // Top
            "radial-gradient(circle at 100% 20%, #0f172a 0%, #000000 100%)", // Blue-ish
            "radial-gradient(circle at 0% 50%, #1a051a 0%, #000000 100%)", // Purple-ish
            "radial-gradient(circle at 100% 80%, #1a1005 0%, #000000 100%)", // Orange-ish
            "radial-gradient(circle at 50% 100%, #1a1a1a 0%, #000000 100%)" // Bottom
        ]
    );

    return (
        <motion.div
            style={{ background }}
            className="fixed inset-0 -z-10 transition-colors duration-1000"
        />
    );
}
