import { Footer } from "@/components/Footer";
import { SceneBuilderHighlight } from "@/components/SceneBuilderHighlight";
import { CreativeHotkeys } from "@/components/CreativeHotkeys";
import { CreativeNative } from "@/components/CreativeNative";
import { NativeEffects } from "@/components/NativeEffects";
import { DynamicBackground } from "@/components/DynamicBackground";
import { CallToAction } from "@/components/CallToAction";
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
    title: "Features",
    description: "Explore the powerful features of Hue Companion: native WinUI 3 performance, DAW-style Scene Builder, 10 native Hue effects, global keyboard shortcuts, AI integration via MCP, and more.",
    openGraph: {
        ...sharedOpenGraph,
        title: "Features — Hue Companion for Windows",
        description: "Native performance, Scene Builder, global hotkeys, AI integration, and more. See everything Hue Companion can do.",
        url: canonicalUrl("/features"),
        images: [{
            url: "/screenshots/scene-builder-dark.png",
            width: 1920,
            height: 1080,
            alt: "Hue Companion Scene Builder — DAW-style timeline editor for animated lighting scenes",
        }],
    },
    twitter: {
        ...sharedTwitter,
        title: "Features — Hue Companion for Windows",
        description: "Native performance, Scene Builder, global hotkeys, AI integration, and more.",
        images: ["/screenshots/scene-builder-dark.png"],
    },
    alternates: {
        canonical: canonicalUrl("/features"),
    },
};

export default function FeaturesPage() {
    return (
        <main className="min-h-screen relative">
            <script
                type="application/ld+json"
                dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationJsonLd) }}
            />
            <script
                type="application/ld+json"
                dangerouslySetInnerHTML={{
                    __html: JSON.stringify(
                        breadcrumbJsonLd([
                            { name: "Home", url: SITE_URL },
                            { name: "Features", url: canonicalUrl("/features") },
                        ])
                    ),
                }}
            />

            <DynamicBackground />

            <div className="pt-32 pb-16 text-center border-b border-white/5 relative z-10 glass-panel">
                <div className="container mx-auto px-4">
                    <h1 className="text-5xl md:text-7xl font-bold tracking-tight mb-6">
                        Features
                    </h1>
                    <p className="text-xl text-zinc-400 max-w-2xl mx-auto">
                        A closer look at what Hue Companion can do.
                    </p>
                </div>
            </div>

            <CreativeNative />
            <SceneBuilderHighlight />
            <NativeEffects />
            <CreativeHotkeys />
            <CallToAction />
            <Footer />
        </main>
    );
}
