import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, type BulletinPost, type ListingSummary } from '../lib/api';

function formatDate(value: string | null) {
  if (!value) {
    return 'Now';
  }

  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
  }).format(new Date(value));
}

export function HomePage() {
  const [bulletin, setBulletin] = useState<BulletinPost | null>(null);
  const [recentBulletins, setRecentBulletins] = useState<BulletinPost[]>([]);
  const [recentListings, setRecentListings] = useState<ListingSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        setLoading(true);
        setError(null);

        const [homeBulletin, bulletins, listings] = await Promise.all([
          api.getHomeBulletin(),
          api.getBulletins(4),
          api.getListings('?page=1&pageSize=4'),
        ]);

        if (!cancelled) {
          setBulletin(homeBulletin);
          setRecentBulletins(bulletins);
          setRecentListings(listings.items);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Unable to load the home page.');
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
      <article className="hero-card hero-layout">
        <div className="hero-copy">
          <p className="eyebrow">Home bulletin</p>
          <h2>{bulletin?.title ?? 'Keep clothes moving through the community.'}</h2>
          <p>
            {bulletin?.body ??
              'Post children\'s clothes, browse what nearby families no longer need, and keep the exchange simple.'}
          </p>
          <div className="button-row">
            <Link className="primary-button" to="/listings">Browse listings</Link>
            <Link className="ghost-button" to="/listings/new">Post an item</Link>
          </div>
        </div>
        <div className="hero-aside">
          <div className="metric-card">
            <span className="info-label">Recent bulletin</span>
            <strong>{bulletin ? formatDate(bulletin.publishedAt) : 'Waiting for first publish'}</strong>
          </div>
          <div className="metric-card">
            <span className="info-label">Latest listings</span>
            <strong>{recentListings.length}</strong>
          </div>
        </div>
      </article>

      {loading ? <p className="status-banner">Loading current bulletin and listings...</p> : null}
      {error ? <p className="status-banner error">{error}</p> : null}

      <section className="page-grid feature-grid">
        <article className="info-card">
          <h3>Simple exchange flow</h3>
          <p>Browse, contact, hand off. No payments and no in-app chat to maintain.</p>
        </article>
        <article className="info-card">
          <h3>Admin-managed content</h3>
          <p>Bulletins, FAQs, and policy pages now come from the live content API.</p>
        </article>
        <article className="info-card">
          <h3>Safety controls</h3>
          <p>Signed-in users can report listings, and admins can moderate users, listings, and content.</p>
        </article>
      </section>

      <section className="page-grid">
        <article className="info-card">
          <div className="section-heading compact-heading">
            <div>
              <p className="eyebrow">Latest bulletins</p>
              <h3>What the community should know</h3>
            </div>
            <Link className="text-link" to="/faq">FAQ</Link>
          </div>
          <div className="stack-list">
            {recentBulletins.length > 0 ? (
              recentBulletins.map((item) => (
                <article key={item.id} className="stack-item">
                  <strong>{item.title}</strong>
                  <p>{item.body}</p>
                </article>
              ))
            ) : (
              <p>No bulletin posts have been published yet.</p>
            )}
          </div>
        </article>

        <article className="info-card">
          <div className="section-heading compact-heading">
            <div>
              <p className="eyebrow">Recent listings</p>
              <h3>Freshly posted</h3>
            </div>
            <Link className="text-link" to="/listings">See all</Link>
          </div>
          <div className="stack-list">
            {recentListings.length > 0 ? (
              recentListings.map((item) => (
                <Link key={item.id} className="listing-preview" to={`/listings/${item.id}`}>
                  <div>
                    <strong>{item.title}</strong>
                    <p>{item.category}{item.size ? ` · Size ${item.size}` : ''}</p>
                  </div>
                  <span>{formatDate(item.createdAt)}</span>
                </Link>
              ))
            ) : (
              <p>No active listings yet.</p>
            )}
          </div>
        </article>
      </section>
    </section>
  );
}
