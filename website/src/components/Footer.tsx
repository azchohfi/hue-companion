export function Footer() {
    return (
        <footer className="border-t border-white/10 bg-zinc-950 py-12">
            <div className="container px-4 md:px-6 flex flex-col md:flex-row justify-between items-center gap-6">
                <div className="flex items-center gap-2">
                    <div className="h-8 w-8 rounded-lg bg-gradient-to-br from-primary to-accent" />
                    <span className="font-bold text-xl tracking-tight">Hue Companion</span>
                </div>

                <p className="text-zinc-500 text-sm">
                    © {new Date().getFullYear()} Hue Companion. Not affiliated with Philips Hue / Signify.
                </p>

                <div className="flex gap-6 text-zinc-400">
                    <a href="#" className="hover:text-white transition-colors">Privacy</a>
                    <a href="#" className="hover:text-white transition-colors">Terms</a>
                    <a href="https://github.com/ddrayne/hue-companion" className="hover:text-white transition-colors">GitHub</a>
                </div>
            </div>
        </footer>
    );
}
