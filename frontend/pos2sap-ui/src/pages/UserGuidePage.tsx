// src/pages/UserGuidePage.tsx
import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { BookOpen, ChevronRight, ExternalLink } from 'lucide-react';
import { useLanguage } from '@/contexts/LanguageContext';
import { getUserGuide } from '@/lib/userGuideContent';
import { cn } from '@/lib/utils';

export default function UserGuidePage() {
  const { lang, t } = useLanguage();
  const doc = useMemo(() => getUserGuide(lang === 'th' ? 'th' : 'en'), [lang]);
  const [activeId, setActiveId] = useState(doc.sections[0]?.id ?? '');

  useEffect(() => {
    setActiveId(doc.sections[0]?.id ?? '');
  }, [doc]);

  useEffect(() => {
    const nodes = doc.sections
      .map((s) => document.getElementById(`guide-${s.id}`))
      .filter((n): n is HTMLElement => !!n);

    if (nodes.length === 0) return;

    const obs = new IntersectionObserver(
      (entries) => {
        const visible = entries
          .filter((e) => e.isIntersecting)
          .sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0];
        if (visible?.target?.id) {
          setActiveId(visible.target.id.replace(/^guide-/, ''));
        }
      },
      { rootMargin: '-20% 0px -55% 0px', threshold: [0.1, 0.35, 0.6] }
    );

    nodes.forEach((n) => obs.observe(n));
    return () => obs.disconnect();
  }, [doc]);

  function scrollTo(id: string) {
    const el = document.getElementById(`guide-${id}`);
    el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    setActiveId(id);
  }

  return (
    <div className="mx-auto max-w-6xl">
      {/* Hero */}
      <section className="relative overflow-hidden rounded-3xl border bg-gradient-to-br from-primary/10 via-card to-sky-500/5 px-6 py-10 shadow-sm dark:from-primary/20 dark:via-card dark:to-sky-950/40 sm:px-10">
        <div className="pointer-events-none absolute -right-16 -top-16 h-56 w-56 rounded-full bg-primary/15 blur-3xl" />
        <div className="pointer-events-none absolute -bottom-20 left-10 h-40 w-40 rounded-full bg-sky-400/10 blur-3xl" />
        <div className="relative flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div className="max-w-2xl">
            <div className="mb-3 inline-flex items-center gap-2 rounded-full border border-primary/20 bg-background/70 px-3 py-1 text-xs font-semibold text-primary backdrop-blur">
              <BookOpen className="h-3.5 w-3.5" />
              {t('userGuide')}
            </div>
            <h1 className="text-3xl font-semibold tracking-tight text-foreground sm:text-4xl">
              {doc.heroTitle}
            </h1>
            <p className="mt-3 text-sm leading-relaxed text-muted-foreground sm:text-base">
              {doc.heroSubtitle}
            </p>
          </div>
        </div>
      </section>

      <div className="mt-8 grid gap-8 lg:grid-cols-[220px_1fr]">
        {/* TOC */}
        <aside className="lg:sticky lg:top-4 lg:self-start">
          <p className="mb-2 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            {doc.tocLabel}
          </p>
          <nav className="flex gap-2 overflow-x-auto pb-2 lg:flex-col lg:overflow-visible lg:pb-0">
            {doc.sections.map((s) => {
              const Icon = s.icon;
              const active = activeId === s.id;
              return (
                <button
                  key={s.id}
                  type="button"
                  onClick={() => scrollTo(s.id)}
                  className={cn(
                    'inline-flex shrink-0 items-center gap-2 rounded-xl border px-3 py-2 text-left text-sm transition',
                    active
                      ? 'border-primary/40 bg-primary text-primary-foreground shadow-sm'
                      : 'border-transparent bg-muted/40 text-muted-foreground hover:border-border hover:bg-muted hover:text-foreground'
                  )}
                >
                  <Icon className="h-3.5 w-3.5 shrink-0" />
                  <span className="truncate font-medium">{s.title}</span>
                </button>
              );
            })}
          </nav>
        </aside>

        {/* Sections */}
        <div className="space-y-6 pb-16">
          {doc.sections.map((s, idx) => {
            const Icon = s.icon;
            return (
              <article
                key={s.id}
                id={`guide-${s.id}`}
                className="scroll-mt-6 rounded-2xl border bg-card p-6 shadow-sm sm:p-8"
              >
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="flex items-start gap-3">
                    <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                      <Icon className="h-5 w-5" />
                    </div>
                    <div>
                      <p className="text-xs font-semibold text-muted-foreground">
                        {String(idx + 1).padStart(2, '0')}
                      </p>
                      <h2 className="text-xl font-semibold tracking-tight">{s.title}</h2>
                      <p className="mt-1 max-w-2xl text-sm leading-relaxed text-muted-foreground">
                        {s.summary}
                      </p>
                    </div>
                  </div>
                  {s.href && (
                    <Link
                      to={s.href}
                      className="inline-flex items-center gap-1.5 rounded-xl border border-primary/30 bg-primary/5 px-3 py-2 text-sm font-medium text-primary transition hover:bg-primary/10"
                    >
                      {doc.openPage}
                      <ExternalLink className="h-3.5 w-3.5" />
                    </Link>
                  )}
                </div>

                {s.steps && s.steps.length > 0 && (
                  <ol className="mt-6 space-y-3">
                    {s.steps.map((step, i) => (
                      <li
                        key={`${s.id}-${i}`}
                        className="flex gap-3 rounded-xl border border-border/70 bg-muted/30 px-4 py-3"
                      >
                        <span className="mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-foreground text-[11px] font-bold text-background">
                          {i + 1}
                        </span>
                        <div className="min-w-0">
                          <p className="font-semibold text-foreground">{step.title}</p>
                          <p className="mt-0.5 text-sm leading-relaxed text-muted-foreground">
                            {step.body}
                          </p>
                        </div>
                      </li>
                    ))}
                  </ol>
                )}

                {s.tips && s.tips.length > 0 && (
                  <div className="mt-5 space-y-2 border-t border-dashed pt-4">
                    {s.tips.map((tip) => (
                      <p
                        key={tip}
                        className="flex gap-2 text-sm leading-relaxed text-amber-800 dark:text-amber-200"
                      >
                        <ChevronRight className="mt-0.5 h-4 w-4 shrink-0 opacity-70" />
                        <span>{tip}</span>
                      </p>
                    ))}
                  </div>
                )}
              </article>
            );
          })}
        </div>
      </div>
    </div>
  );
}
