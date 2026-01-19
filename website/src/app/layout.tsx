import type { Metadata } from "next";
import { Inter, Space_Grotesk } from "next/font/google";
import "./globals.css";
import { cn } from "@/lib/utils";

const inter = Inter({
  subsets: ["latin"],
  variable: "--font-inter",
  display: "swap",
});

const spaceGrotesk = Space_Grotesk({
  subsets: ["latin"],
  variable: "--font-space-grotesk",
  display: "swap",
});

export const metadata: Metadata = {
  title: "Hue Companion for Windows",
  description: "The most advanced Philips Hue controller for Windows. Native, fast, and beautiful.",
};

import { Navbar } from "@/components/Navbar";

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className={cn(inter.variable, spaceGrotesk.variable, "antialiased")}>
      <body className="bg-background text-foreground min-h-screen selection:bg-primary/20 selection:text-primary">
        <Navbar />
        {children}
      </body>
    </html>
  );
}
