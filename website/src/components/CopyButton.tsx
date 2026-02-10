"use client";

import { useState } from "react";
import { Check, Copy } from "lucide-react";

interface CopyButtonProps {
    text: string;
    label?: string;
    className?: string;
}

export function CopyButton({ text, label = "Copy", className = "" }: CopyButtonProps) {
    const [copied, setCopied] = useState(false);

    const handleCopy = async () => {
        await navigator.clipboard.writeText(text);
        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
    };

    return (
        <button
            onClick={handleCopy}
            className={`inline-flex items-center gap-2 px-3 py-1.5 rounded-lg text-sm font-medium transition-colors ${
                copied
                    ? "bg-green-500/20 text-green-400 border border-green-500/30"
                    : "bg-zinc-800 text-zinc-300 border border-zinc-700 hover:bg-zinc-700 hover:text-white"
            } ${className}`}
        >
            {copied ? (
                <>
                    <Check className="w-3.5 h-3.5" />
                    Copied!
                </>
            ) : (
                <>
                    <Copy className="w-3.5 h-3.5" />
                    {label}
                </>
            )}
        </button>
    );
}
