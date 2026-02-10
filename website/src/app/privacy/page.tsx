import { Footer } from "@/components/Footer";
import { Metadata } from "next";
import { canonicalUrl } from "@/lib/seo";

export const metadata: Metadata = {
    title: "Privacy Policy",
    description: "Privacy policy for Hue Companion for Windows. We do not collect, store, or transmit any personal data.",
    alternates: {
        canonical: canonicalUrl("/privacy"),
    },
};

export default function PrivacyPage() {
    return (
        <main className="min-h-screen bg-black text-zinc-200 font-sans selection:bg-purple-900/50">
            <div className="max-w-3xl mx-auto px-6 pt-32 pb-20">
                <header className="mb-20 border-b border-zinc-800 pb-10">
                    <h1 className="text-4xl md:text-5xl font-bold text-white mb-6">Privacy Policy</h1>
                    <p className="text-xl text-zinc-400 leading-relaxed">
                        Last updated: February 10, 2026
                    </p>
                </header>

                <article className="prose prose-invert prose-zinc max-w-none">
                    <section className="mb-12">
                        <h2 className="text-2xl font-bold text-white mb-4">Summary</h2>
                        <p className="text-zinc-300">
                            Hue Companion for Windows does not collect, store, or transmit any personal data. The app operates entirely on your local network and does not communicate with any external servers.
                        </p>
                    </section>

                    <section className="mb-12">
                        <h2 className="text-2xl font-bold text-white mb-4">Data We Do Not Collect</h2>
                        <p className="text-zinc-300 mb-4">
                            Hue Companion does not collect or transmit:
                        </p>
                        <ul className="list-disc pl-6 space-y-2 text-zinc-400">
                            <li>Personal information (name, email, address)</li>
                            <li>Usage analytics or telemetry</li>
                            <li>Crash reports</li>
                            <li>Device identifiers</li>
                            <li>Location data</li>
                            <li>Any data to third-party services</li>
                        </ul>
                    </section>

                    <section className="mb-12">
                        <h2 className="text-2xl font-bold text-white mb-4">Local Network Communication</h2>
                        <p className="text-zinc-300 mb-4">
                            Hue Companion communicates exclusively with Philips Hue Bridges on your local network using the Hue CLIP v2 API. This communication stays within your local network and is used solely to control your lights, retrieve room/zone configurations, and manage scenes.
                        </p>
                        <p className="text-zinc-300">
                            The app requires the <code className="bg-zinc-800 px-1.5 py-0.5 rounded text-sm">privateNetworkClientServer</code> capability to discover and communicate with Hue Bridges on your network.
                        </p>
                    </section>

                    <section className="mb-12">
                        <h2 className="text-2xl font-bold text-white mb-4">Local Data Storage</h2>
                        <p className="text-zinc-300 mb-4">
                            Hue Companion stores the following data locally on your device:
                        </p>
                        <ul className="list-disc pl-6 space-y-2 text-zinc-400">
                            <li><strong className="text-zinc-300">Bridge connection details</strong> — IP addresses and API keys for your Hue Bridges, stored in your local app data folder</li>
                            <li><strong className="text-zinc-300">App settings</strong> — Your preferences such as theme, hotkey bindings, and dashboard layout</li>
                            <li><strong className="text-zinc-300">Custom scenes</strong> — Animated scenes you create in the Scene Builder, saved as JSON files</li>
                        </ul>
                        <p className="text-zinc-300 mt-4">
                            All data is stored in <code className="bg-zinc-800 px-1.5 py-0.5 rounded text-sm">%LOCALAPPDATA%\HueCompanion</code> and never leaves your device.
                        </p>
                    </section>

                    <section className="mb-12">
                        <h2 className="text-2xl font-bold text-white mb-4">MCP Server</h2>
                        <p className="text-zinc-300">
                            The optional MCP (Model Context Protocol) server runs locally on your device and allows AI assistants to control your lights. It communicates only with your local Hue Bridges and the AI client running on your machine. No data is sent to external servers by the MCP server itself.
                        </p>
                    </section>

                    <section className="mb-12">
                        <h2 className="text-2xl font-bold text-white mb-4">Third-Party Services</h2>
                        <p className="text-zinc-300">
                            Hue Companion does not integrate with any third-party analytics, advertising, or tracking services. The app has no accounts, no sign-in, and no cloud features.
                        </p>
                    </section>

                    <section className="mb-12">
                        <h2 className="text-2xl font-bold text-white mb-4">Changes to This Policy</h2>
                        <p className="text-zinc-300">
                            If we ever change our data practices, this policy will be updated with a new date. Since the app collects no data, we do not anticipate significant changes.
                        </p>
                    </section>

                    <section>
                        <h2 className="text-2xl font-bold text-white mb-4">Contact</h2>
                        <p className="text-zinc-300">
                            If you have questions about this privacy policy, you can open an issue on our{" "}
                            <a href="https://github.com/ddrayne/hue-companion" className="text-purple-400 hover:text-purple-300 transition-colors">
                                GitHub repository
                            </a>.
                        </p>
                    </section>
                </article>
            </div>

            <Footer />
        </main>
    );
}
