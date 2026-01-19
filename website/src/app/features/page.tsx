import { CompetitorComparison } from "@/components/CompetitorComparison";
import { Footer } from "@/components/Footer";
import { SceneBuilderHighlight } from "@/components/SceneBuilderHighlight";
import { CreativeHotkeys } from "@/components/CreativeHotkeys";
import { CreativeNative } from "@/components/CreativeNative";
import { LuminaAI } from "@/components/LuminaAI";
import { DynamicBackground } from "@/components/DynamicBackground";
import { CallToAction } from "@/components/CallToAction";
import { Metadata } from "next";

export const metadata: Metadata = {
    title: "Features - Hue Companion for Windows",
    description: "Explore the powerful features of Hue Companion: Native performance, Zones, Shortcuts, and more.",
};

export default function FeaturesPage() {
    return (
        <main className="min-h-screen relative">
            <DynamicBackground />

            <div className="pt-32 pb-16 text-center border-b border-white/5 relative z-10 glass-panel">
                <div className="container px-4">
                    <h1 className="text-5xl md:text-7xl font-bold tracking-tight mb-6">
                        Features
                    </h1>
                    <p className="text-xl text-zinc-400 max-w-2xl mx-auto">
                        A deep dive into what makes this the best Hue experience on Windows.
                    </p>
                </div>
            </div>

            <CompetitorComparison />

            <CreativeNative />

            <SceneBuilderHighlight />

            <CreativeHotkeys />

            <LuminaAI />

            <CallToAction />

            <Footer />


        </main>
    );
}
