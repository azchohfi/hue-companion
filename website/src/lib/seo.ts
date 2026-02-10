import type { Metadata } from "next";

// Site constants
export const SITE_URL = "https://hue-companion.apps.drayne.xyz";
export const SITE_NAME = "Hue Companion for Windows";
export const SITE_DESCRIPTION =
    "The most advanced Philips Hue controller for Windows. Native WinUI 3 performance, Scene Builder, global hotkeys, AI integration, and more.";
export const AUTHOR_NAME = "Daniel Drayne";
export const GITHUB_URL = "https://github.com/ddrayne/hue-companion";
export const STORE_URL = "https://apps.microsoft.com/detail/9PNC5CQQ403T";
export const DEFAULT_OG_IMAGE = `/screenshots/home-dark.png`;

// Helper to build canonical URLs consistently
export function canonicalUrl(path: string = ""): string {
    return `${SITE_URL}${path}`;
}

// Shared OpenGraph defaults (spread into each page's openGraph)
export const sharedOpenGraph: Metadata["openGraph"] = {
    siteName: SITE_NAME,
    locale: "en_US",
    type: "website",
    images: [
        {
            url: DEFAULT_OG_IMAGE,
            width: 1920,
            height: 1080,
            alt: "Hue Companion for Windows — dashboard showing rooms with live light status",
        },
    ],
};

// Shared Twitter defaults
export const sharedTwitter: Metadata["twitter"] = {
    card: "summary_large_image",
    images: [DEFAULT_OG_IMAGE],
};

// Reusable SoftwareApplication JSON-LD
export const softwareApplicationJsonLd = {
    "@context": "https://schema.org",
    "@type": "SoftwareApplication",
    name: SITE_NAME,
    operatingSystem: "Windows 10, Windows 11",
    applicationCategory: "UtilitiesApplication",
    applicationSubCategory: "Smart Home Controller",
    offers: { "@type": "Offer", price: "0", priceCurrency: "USD" },
    featureList: [
        "Native WinUI 3 Performance",
        "Global Keyboard Shortcuts",
        "Multi-Bridge Support",
        "Zone & Room Control",
        "Native Hue Effects (10 built-in)",
        "DAW-Style Animated Scene Builder",
        "Custom Dashboard",
        "System Tray Support",
        "AI Integration via Model Context Protocol (MCP)",
        "Dark and Light Themes",
    ],
    author: { "@type": "Person", name: AUTHOR_NAME },
    description: SITE_DESCRIPTION,
    url: SITE_URL,
    downloadUrl: STORE_URL,
    screenshot: `${SITE_URL}/screenshots/home-dark.png`,
    softwareRequirements: "Windows 10 version 1809 or later, Philips Hue Bridge (v2)",
    fileSize: "65MB",
    memoryRequirements: "40MB RAM",
    programmingLanguage: "C#",
};

// Breadcrumb JSON-LD helper
export function breadcrumbJsonLd(items: { name: string; url: string }[]) {
    return {
        "@context": "https://schema.org",
        "@type": "BreadcrumbList",
        itemListElement: items.map((item, index) => ({
            "@type": "ListItem",
            position: index + 1,
            name: item.name,
            item: item.url,
        })),
    };
}
