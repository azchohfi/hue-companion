import { Footer } from "@/components/Footer";
import { Metadata } from "next";
import {
    SITE_URL,
    sharedOpenGraph,
    sharedTwitter,
    softwareApplicationJsonLd,
    breadcrumbJsonLd,
    canonicalUrl,
} from "@/lib/seo";

export const metadata: Metadata = {
    title: "About",
    description: "Technical specifications, FAQ, and everything you need to know about Hue Companion for Windows 10 and 11. Free native Philips Hue controller with Scene Builder, global hotkeys, and AI integration.",
    openGraph: {
        ...sharedOpenGraph,
        title: "About Hue Companion for Windows",
        description: "Technical specifications, FAQ, and everything you need to know about Hue Companion — the free native Philips Hue controller for Windows.",
        url: canonicalUrl("/about"),
    },
    twitter: {
        ...sharedTwitter,
        title: "About Hue Companion for Windows",
        description: "Technical specifications, FAQ, and everything you need to know about Hue Companion — the free native Philips Hue controller for Windows.",
    },
    alternates: {
        canonical: canonicalUrl("/about"),
    },
};

const faqItems = [
    {
        question: "What is Hue Companion?",
        answer: "Hue Companion is a free, native Windows desktop application for controlling Philips Hue smart lights. Built with WinUI 3 (the same framework as Windows 11 system apps), it offers fast startup under half a second, low memory usage around 40MB, and deep Windows integration including global keyboard shortcuts, system tray support, and native dark and light themes.",
    },
    {
        question: "How does Hue Companion compare to other Hue apps for Windows?",
        answer: "Unlike Electron-based alternatives, Hue Companion is built natively with WinUI 3 and C#. This means it uses approximately 40MB of RAM compared to 400MB or more for Electron apps, starts in under half a second, and integrates with Windows features like Mica and Acrylic materials, system tray, and global hotkeys. It also includes unique features like a DAW-style Scene Builder and AI integration via MCP.",
    },
    {
        question: "Is Hue Companion free?",
        answer: "Yes, Hue Companion is completely free. All features including the Scene Builder, native effects, multi-bridge support, global hotkeys, AI integration, and custom dashboard are included at no cost.",
    },
    {
        question: "Does Hue Companion work with Windows 10?",
        answer: "Yes, Hue Companion supports Windows 10 version 1809 and newer, as well as all versions of Windows 11. It is available for both x64 (Intel/AMD) and ARM64 processors.",
    },
    {
        question: "What are the system requirements for Hue Companion?",
        answer: "Hue Companion requires Windows 10 version 1809 or later, or any version of Windows 11. You also need a Philips Hue Bridge (v2) connected to your local network. The installer is approximately 65MB and the app uses about 40MB of RAM while running.",
    },
    {
        question: "Is a Philips Hue Bridge required?",
        answer: "Yes, a Philips Hue Bridge (v2) connected to your local network is required. Bluetooth-only Hue bulbs are not currently supported. The app communicates directly with the bridge using the Hue CLIP v2 API.",
    },
    {
        question: "Can I use multiple Hue Bridges?",
        answer: "Yes, Hue Companion supports connecting to multiple Hue Bridges simultaneously. All rooms, zones, and lights from every bridge appear together in one unified interface.",
    },
    {
        question: "What is the Scene Builder in Hue Companion?",
        answer: "The Scene Builder is a DAW-style timeline editor for creating animated lighting scenes. You can place keyframes on per-light tracks to define exact colors and brightness at specific times, add event triggers like lightning flashes, sparkles, or candle flickers, and preview animations on your real lights in real-time. Scenes are saved locally and can be replayed anytime.",
    },
    {
        question: "What is the MCP server in Hue Companion?",
        answer: "Hue Companion includes a built-in MCP (Model Context Protocol) server that lets AI assistants like Claude Desktop, Claude Code, and VS Code Copilot control your Philips Hue lights using natural language. It is the easiest way to control your Hue lights via MCP on Windows. You can say things like \"set the bedroom to a warm sunset\" or \"create a relaxing animation\" and the AI will control your lights through Hue Companion. Setup takes just a few clicks from the Settings page.",
    },
    {
        question: "What native Hue effects does Hue Companion support?",
        answer: "Hue Companion supports all 10 built-in Hue effects powered by the bridge's native engine: Fire, Candle, Sparkle, Glisten, Opal, Prism, Underwater, Cosmos, Sunbeam, and Enchant. Each effect can be adjusted for speed and brightness, and applied to individual lights, rooms, or zones.",
    },
    {
        question: "Does Hue Companion collect any data or require an account?",
        answer: "No. Hue Companion does not collect, store, or transmit any personal data. It communicates only with Philips Hue Bridges on your local network. There are no accounts, no analytics, no telemetry, and no cloud features. All settings and custom scenes are stored locally on your device.",
    },
    {
        question: "Can I control my lights with keyboard shortcuts?",
        answer: "Yes. Hue Companion supports global keyboard shortcuts that work system-wide, even when the app is minimized or running in the system tray. You can map any scene activation, room toggle, or brightness adjustment to a custom key combination for instant control from anywhere on your desktop.",
    },
];

const faqJsonLd = {
    "@context": "https://schema.org",
    "@type": "FAQPage",
    mainEntity: faqItems.map((item) => ({
        "@type": "Question",
        name: item.question,
        acceptedAnswer: {
            "@type": "Answer",
            text: item.answer,
        },
    })),
};

export default function AboutPage() {
    return (
        <main className="min-h-screen bg-black text-zinc-200 font-sans selection:bg-purple-900/50">
            <script
                type="application/ld+json"
                dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationJsonLd) }}
            />
            <script
                type="application/ld+json"
                dangerouslySetInnerHTML={{ __html: JSON.stringify(faqJsonLd) }}
            />
            <script
                type="application/ld+json"
                dangerouslySetInnerHTML={{
                    __html: JSON.stringify(
                        breadcrumbJsonLd([
                            { name: "Home", url: SITE_URL },
                            { name: "About", url: canonicalUrl("/about") },
                        ])
                    ),
                }}
            />

            <div className="max-w-3xl mx-auto px-6 pt-32 pb-20">
                <header className="mb-20 border-b border-zinc-800 pb-10">
                    <h1 className="text-4xl md:text-5xl font-bold text-white mb-6">About Hue Companion</h1>
                    <p className="text-xl text-zinc-400 leading-relaxed">
                        Technical details and FAQ for the native Windows client for Philips Hue.
                    </p>
                </header>

                <article className="prose prose-invert prose-zinc max-w-none">
                    <section className="mb-16">
                        <h2 className="text-2xl font-bold text-white mb-4">What is Hue Companion?</h2>
                        <p className="text-zinc-300 mb-4">
                            <strong>Hue Companion for Windows</strong> is a native desktop application designed to control Philips Hue lighting systems. Unlike other solutions that rely on web technologies (Electron), Hue Companion is built on the <strong>Windows App SDK (WinUI 3)</strong>, ensuring essentially zero startup latency and minimal memory footprint.
                        </p>
                        <p className="text-zinc-300">
                            It bridges the gap between smart home lighting and PC-based workflows, allowing gamers, developers, and power users to synchronize their environment with their desktop activity.
                        </p>
                    </section>

                    <section className="mb-16">
                        <h2 className="text-2xl font-bold text-white mb-6">Key Capabilities</h2>
                        <dl className="grid gap-8 md:grid-cols-2">
                            <div className="bg-zinc-900/50 p-6 rounded-xl border border-zinc-800">
                                <dt className="text-white font-bold mb-2">Native Architecture</dt>
                                <dd className="text-zinc-400 text-sm">Built with C# and WinUI 3. Deeply integrated with Windows 11 design language (Mica, Acrylic).</dd>
                            </div>
                            <div className="bg-zinc-900/50 p-6 rounded-xl border border-zinc-800">
                                <dt className="text-white font-bold mb-2">Global Hotkeys</dt>
                                <dd className="text-zinc-400 text-sm">System-level keyboard hooks allow for lighting control even when the app is in the background or minimized.</dd>
                            </div>
                            <div className="bg-zinc-900/50 p-6 rounded-xl border border-zinc-800">
                                <dt className="text-white font-bold mb-2">Multi-Bridge Support</dt>
                                <dd className="text-zinc-400 text-sm">Connect and manage multiple Hue Bridges simultaneously. All rooms and zones from every bridge appear in one unified interface.</dd>
                            </div>
                            <div className="bg-zinc-900/50 p-6 rounded-xl border border-zinc-800">
                                <dt className="text-white font-bold mb-2">Scene Builder</dt>
                                <dd className="text-zinc-400 text-sm">DAW-style timeline editor for creating complex, multi-track animated lighting scenes with keyframes and event triggers.</dd>
                            </div>
                            <div className="bg-zinc-900/50 p-6 rounded-xl border border-zinc-800">
                                <dt className="text-white font-bold mb-2">Native Hue Effects</dt>
                                <dd className="text-zinc-400 text-sm">10 built-in effects (fire, candle, sparkle, prism, cosmos, and more) with adjustable speed and brightness controls.</dd>
                            </div>
                            <div className="bg-zinc-900/50 p-6 rounded-xl border border-zinc-800">
                                <dt className="text-white font-bold mb-2">Custom Dashboard</dt>
                                <dd className="text-zinc-400 text-sm">Pin favorite rooms, zones, and individual lights to a personalized dashboard for instant one-tap access.</dd>
                            </div>
                        </dl>
                    </section>

                    <section className="mb-16">
                        <h2 className="text-2xl font-bold text-white mb-6">Frequently Asked Questions</h2>
                        <div className="space-y-8">
                            {faqItems.map((item) => (
                                <div key={item.question}>
                                    <h3 className="text-lg font-bold text-white mb-2">{item.question}</h3>
                                    <p className="text-zinc-400">{item.answer}</p>
                                </div>
                            ))}
                        </div>
                    </section>

                    <section>
                        <h2 className="text-2xl font-bold text-white mb-6">Technical Specifications</h2>
                        <div className="overflow-hidden rounded-xl border border-zinc-800">
                            <table className="w-full text-left text-sm">
                                <tbody className="divide-y divide-zinc-800 bg-zinc-900/30">
                                    <tr>
                                        <th className="px-6 py-4 font-medium text-zinc-300 w-1/3">Framework</th>
                                        <td className="px-6 py-4 text-zinc-400">Windows App SDK (WinUI 3)</td>
                                    </tr>
                                    <tr>
                                        <th className="px-6 py-4 font-medium text-zinc-300">Language</th>
                                        <td className="px-6 py-4 text-zinc-400">C# / .NET 8</td>
                                    </tr>
                                    <tr>
                                        <th className="px-6 py-4 font-medium text-zinc-300">Memory Usage</th>
                                        <td className="px-6 py-4 text-zinc-400">~40 MB</td>
                                    </tr>
                                    <tr>
                                        <th className="px-6 py-4 font-medium text-zinc-300">Startup Time</th>
                                        <td className="px-6 py-4 text-zinc-400">&lt;0.5 seconds</td>
                                    </tr>
                                    <tr>
                                        <th className="px-6 py-4 font-medium text-zinc-300">Installer Size</th>
                                        <td className="px-6 py-4 text-zinc-400">~65 MB</td>
                                    </tr>
                                    <tr>
                                        <th className="px-6 py-4 font-medium text-zinc-300">Supported OS</th>
                                        <td className="px-6 py-4 text-zinc-400">Windows 10 (1809+), Windows 11</td>
                                    </tr>
                                    <tr>
                                        <th className="px-6 py-4 font-medium text-zinc-300">Architectures</th>
                                        <td className="px-6 py-4 text-zinc-400">x64, ARM64</td>
                                    </tr>
                                    <tr>
                                        <th className="px-6 py-4 font-medium text-zinc-300">Price</th>
                                        <td className="px-6 py-4 text-zinc-400">Free</td>
                                    </tr>
                                </tbody>
                            </table>
                        </div>
                    </section>
                </article>
            </div>

            <Footer />
        </main>
    );
}
