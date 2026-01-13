import Link from 'next/link';
import { Play } from 'lucide-react';

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-background flex flex-col">
      {/* Header */}
      <header className="p-6">
        <Link href="/" className="flex items-center gap-2 w-fit">
          <div className="w-10 h-10 bg-primary rounded-lg flex items-center justify-center">
            <Play className="w-6 h-6 text-white" fill="white" />
          </div>
          <span className="text-xl font-bold text-white">StreamVision</span>
        </Link>
      </header>

      {/* Content */}
      <main className="flex-1 flex items-center justify-center p-4">
        {children}
      </main>

      {/* Footer */}
      <footer className="p-6 text-center">
        <p className="text-sm text-muted-foreground">
          © 2024 StreamVision. Tous droits reserves.
        </p>
      </footer>
    </div>
  );
}
