"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import { motion } from "framer-motion";

export function Navbar() {
    const pathname = usePathname();

    const links = [
        { href: "/", label: "Home" },
        { href: "/features", label: "Features" },
        { href: "/about", label: "About" },
        { href: "/mcp-setup", label: "MCP Setup" },
    ];

    return (
        <motion.header
            initial={{ y: -100 }}
            animate={{ y: 0 }}
            className="fixed top-0 left-0 right-0 z-50 border-b border-white/5 bg-black/50 backdrop-blur-xl"
        >
            <div className="container mx-auto px-4 md:px-6 h-16 flex items-center justify-between">
                <Link href="/" className="flex items-center gap-2">
                    <div className="h-6 w-6 rounded-md bg-gradient-to-br from-primary to-accent" />
                    <span className="font-bold tracking-tight">Hue Companion</span>
                </Link>

                <nav className="flex gap-6">
                    {links.map((link) => (
                        <Link
                            key={link.href}
                            href={link.href}
                            className={cn(
                                "text-sm font-medium transition-colors hover:text-white",
                                pathname === link.href ? "text-white" : "text-zinc-400"
                            )}
                        >
                            {link.label}
                        </Link>
                    ))}
                    <a href="https://github.com/ddrayne/hue-companion" className="text-sm font-medium text-zinc-400 hover:text-white transition-colors">GitHub</a>
                </nav>
            </div>
        </motion.header>
    );
}
