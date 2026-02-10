import { Features } from "@/components/Features";
import { Footer } from "@/components/Footer";
import { Hero } from "@/components/Hero";
import { McpShowcase } from "@/components/McpShowcase";
import { Showcase } from "@/components/Showcase";

export default function Home() {
  return (
    <main className="min-h-screen bg-background">
      <Hero />
      <Features />
      <McpShowcase />
      <Showcase />
      <Footer />
    </main>
  );
}
