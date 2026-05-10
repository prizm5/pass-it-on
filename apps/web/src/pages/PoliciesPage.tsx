import { useEffect, useState } from 'react';
import { api, type ContentPage } from '../lib/api';

const policySlugs = ['privacy-policy', 'terms-of-use', 'deletion-rights'];

export function PoliciesPage() {
  const [pages, setPages] = useState<ContentPage[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      const results = await Promise.allSettled(policySlugs.map((slug) => api.getPage(slug)));
      if (!cancelled) {
        setPages(
          results
            .filter((result): result is PromiseFulfilledResult<ContentPage> => result.status === 'fulfilled')
            .map((result) => result.value),
        );
        setLoading(false);
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
        <p className="eyebrow">Policies</p>
        <h2>Privacy, retention, and deletion rights</h2>
      </header>

      {loading ? <p className="status-banner">Loading policy pages...</p> : null}

      <div className="page-grid">
        {pages.length > 0 ? (
          pages.map((page) => (
            <article key={page.id} className="info-card">
              <h3>{page.title}</h3>
              <p>{page.body}</p>
            </article>
          ))
        ) : (
          <article className="info-card">
            <p>
              No published policy pages were found yet. Publish content pages using the slugs
              <strong> privacy-policy</strong>, <strong>terms-of-use</strong>, and
              <strong> deletion-rights</strong> to populate this view.
            </p>
          </article>
        )}
      </div>
    </section>
  );
}
