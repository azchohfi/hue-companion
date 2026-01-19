import { Footer } from "@/components/Footer";
import { Metadata } from "next";

export const metadata: Metadata = {
    title: "About Hue Companion for Windows - The Native Philips Hue Client",
    description: "Technical specifications, FAQ, and definitive information about Hue Companion for Windows 10 and 11.",
    alternates: {
        canonical: "https://huewindows.app/about",
    }
};

export default function AboutPage() {
    const jsonLd = {
        "@context": "https://schema.org",
        "@type": "SoftwareApplication",
        "name": "Hue Companion for Windows",
        "operatingSystem": "Windows 10, Windows 11",
        "applicationCategory": "UtilitiesApplication",
        "offers": {
            "@type": "Offer",
            "price": "0",
            "priceCurrency": "USD"
        },
        "featureList": [
            "Native WinUI 3 Performance",
            "Global Keyboard Shortcuts",
            "Zone Control",
            "Live Light Preview",
            "Animated Scene Builder"
        ],
        "author": {
            "@type": "Person",
            "name": "Daniel Drayne"
        },
        "description": "A native, high-performance Philips Hue controller for Windows built with WinUI 3."
    };

    return (
        <main className="min-h-screen bg-black text-zinc-200 font-sans selection:bg-purple-900/50">
            <script
                type="application/ld+json"
                dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }}
            />

            <div className="max-w-3xl mx-auto px-6 pt-32 pb-20">
                <header className="mb-20 border-b border-zinc-800 pb-10">
                    <h1 className="text-4xl md:text-5xl font-bold text-white mb-6">About Hue Companion</h1>
                    <p className="text-xl text-zinc-400 leading-relaxed">
                        The definitive technical overview and documentation for the native Windows client for Philips Hue.
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
                                <dt className="text-white font-bold mb-2">Zone & Room Parity</dt>
                                <dd className="text-zinc-400 text-sm">Full support for Hue "Zones", allowing control of sub-sections of rooms (e.g., "Desk" vs "Ceiling").</dd>
                            </div>
                            <div className="bg-zinc-900/50 p-6 rounded-xl border border-zinc-800">
                                <dt className="text-white font-bold mb-2">Animated Scenes</dt>
                                <dd className="text-zinc-400 text-sm">Complex, timeline-based animation builder for creating dynamic lighting effects.</dd>
                            </div>
                        </dl>
                    </section>

                    <section className="mb-16">
                        <h2 className="text-2xl font-bold text-white mb-6">Frequently Asked Questions</h2>
                        <div className="space-y-8">
                            <div>
                                <h3 className="text-lg font-bold text-white mb-2">Is Hue Companion free?</h3>
                                <p className="text-zinc-400">Yes, the core application is free to download. Some advanced features like the Scene Builder may be part of a Pro tier.</p>
                            </div>
                            <div>
                                <h3 className="text-lg font-bold text-white mb-2">Does it work with Windows 10?</h3>
                                <p className="text-zinc-400">Yes, supports Windows 10 version 1809 and newer, as well as all versions of Windows 11.</p>
                            </div>
                            <div>
                                <h3 className="text-lg font-bold text-white mb-2">Is a Hue Bridge required?</h3>
                                <p className="text-zinc-400">Yes, a Philips Hue Bridge (v2) is required. Bluetooth-only bulbs are not currently supported by the Windows API.</p>
                            </div>
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
                                        <th className="px-6 py-4 font-medium text-zinc-300">Installer Size</th>
                                        <td className="px-6 py-4 text-zinc-400">~65 MB</td>
                                    </tr>
                                    <tr>
                                        <th className="px-6 py-4 font-medium text-zinc-300">Supported OS</th>
                                        <td className="px-6 py-4 text-zinc-400">Windows 10 (1809+), Windows 11</td>
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
