import type { Metadata } from 'next';
import { Inter } from 'next/font/google';
import './globals.css';

const inter = Inter({ subsets: ['latin'] });

export const metadata: Metadata = {
  title: 'Visiora - Votre univers de streaming',
  description: 'Regardez vos chaines TV, films et series preferes en streaming. Interface moderne, EPG integre, multi-plateforme.',
  keywords: ['IPTV', 'streaming', 'TV en direct', 'films', 'series', 'VOD'],
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="fr" className="dark">
      <body className={`${inter.className} min-h-screen bg-background`}>
        {children}
      </body>
    </html>
  );
}
