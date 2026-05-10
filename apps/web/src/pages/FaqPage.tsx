import { useEffect, useState } from 'react';
import { api, type ContentPage } from '../lib/api';

export function FaqPage() {
  const [page, setPage] = useState<ContentPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        setLoading(true);
        setError(null);
        const payload = await api.getPage('faq');
        if (!cancelled) {
          setPage(payload);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'FAQ page is not available yet.');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <section className="page-stack">
      <header className="section-heading">
        <p className="eyebrow">FAQ</p>
        <h2>Frequently asked questions</h2>
      </header>
      <article className="info-card">
        {loading ? <p>Loading FAQ content...</p> : null}
        {error ? (
          <p>
            {error} Ask admins to publish a content page with the slug <strong>faq</strong>.
          </p>
        ) : null}
        {page ? (
          <>
            <h3>{page.title}</h3>
            <p>{page.body}</p>
          </>
        ) : null}
      </article>
    </section>
  );
}
