import type { Metadata } from "next";
import { Features } from "@/components/Features";
import { Footer } from "@/components/Footer";
import { Hero } from "@/components/Hero";
import { McpShowcase } from "@/components/McpShowcase";
import { Showcase } from "@/components/Showcase";
import {
    SITE_URL,
    SITE_NAME,
    SITE_DESCRIPTION,
    STORE_URL,
    sharedOpenGraph,
    sharedTwitter,
    softwareApplicationJsonLd,
} from "@/lib/seo";

export const metadata: Metadata = {
    title: `${SITE_NAME} — The Native Philips Hue Controller`,
    description: SITE_DESCRIPTION,
    openGraph: {
        ...sharedOpenGraph,
        title: `${SITE_NAME} — The Native Philips Hue Controller`,
        description: SITE_DESCRIPTION,
        url: SITE_URL,
    },
    twitter: {
        ...sharedTwitter,
        title: `${SITE_NAME} — The Native Philips Hue Controller`,
        description: SITE_DESCRIPTION,
    },
    alternates: {
        canonical: SITE_URL,
    },
};

const websiteJsonLd = {
    "@context": "https://schema.org",
    "@type": "WebSite",
    name: SITE_NAME,
    url: SITE_URL,
    description: SITE_DESCRIPTION,
    potentialAction: {
        "@type": "SearchAction",
        target: `${STORE_URL}`,
        "query-input": undefined,
    },
};

export default function Home() {
    return (
        <main className="min-h-screen bg-background">
            <script
                type="application/ld+json"
                dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationJsonLd) }}
            />
            <script
                type="application/ld+json"
                dangerouslySetInnerHTML={{ __html: JSON.stringify(websiteJsonLd) }}
            />
            <Hero />
            <Features />
            <McpShowcase />
            <Showcase />
            <Footer />
        </main>
    );
}
