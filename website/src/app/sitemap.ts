import type { MetadataRoute } from "next";
import { SITE_URL } from "@/lib/seo";

export default function sitemap(): MetadataRoute.Sitemap {
    return [
        { url: SITE_URL, lastModified: new Date(), changeFrequency: "weekly", priority: 1 },
        { url: `${SITE_URL}/features`, lastModified: new Date(), changeFrequency: "monthly", priority: 0.9 },
        { url: `${SITE_URL}/about`, lastModified: new Date(), changeFrequency: "monthly", priority: 0.8 },
        { url: `${SITE_URL}/mcp-setup`, lastModified: new Date(), changeFrequency: "monthly", priority: 0.7 },
        { url: `${SITE_URL}/privacy`, lastModified: new Date(), changeFrequency: "yearly", priority: 0.3 },
    ];
}
