import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { api, type ListingSummary, type PagedResponse } from '../lib/api';

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(new Date(value));
}

export function ListingsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [data, setData] = useState<PagedResponse<ListingSummary> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const q = searchParams.get('q') ?? '';
  const category = searchParams.get('category') ?? '';
  const page = Number(searchParams.get('page') ?? '1');

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        setLoading(true);
        setError(null);
        const query = new URLSearchParams();
        query.set('page', String(page || 1));
        query.set('pageSize', '12');
        if (q) {
          query.set('q', q);
        }
        if (category) {
          query.set('category', category);
        }

        const payload = await api.getListings(`?${query.toString()}`);
        if (!cancelled) {
          setData(payload);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Unable to load listings.');
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
  }, [category, page, q]);

  function updateSearch(next: Record<string, string>) {
    const params = new URLSearchParams(searchParams);
    Object.entries(next).forEach(([key, value]) => {
      if (value) {
        params.set(key, value);
      } else {
        params.delete(key);
      }
    });
    params.set('page', '1');
    setSearchParams(params);
  }

  return (
    <section className="page-stack">
      <header className="section-heading">
        <p className="eyebrow">Listings</p>
        <h2>Browse and search exchange items</h2>
        <p className="section-copy">
          Filter by keyword and category. The feed only shows active items available for exchange.
        </p>
      </header>

      <article className="info-card filter-card">
        <div className="filter-grid">
          <label>
            <span>Search</span>
            <input
              value={q}
              onChange={(event) => updateSearch({ q: event.target.value })}
              placeholder="Coat, rain boots, pajamas"
            />
          </label>
          <label>
            <span>Category</span>
            <input
              value={category}
              onChange={(event) => updateSearch({ category: event.target.value })}
              placeholder="Outerwear, Shoes, Tops"
            />
          </label>
          <div className="filter-actions">
            <Link className="ghost-button" to="/listings/new">Post an item</Link>
            <button className="secondary-button" type="button" onClick={() => setSearchParams(new URLSearchParams())}>
              Clear
            </button>
          </div>
        </div>
      </article>

      {loading ? <p className="status-banner">Loading listings...</p> : null}
      {error ? <p className="status-banner error">{error}</p> : null}

      <div className="listing-grid">
        {data?.items.map((item) => (
          <Link key={item.id} className="listing-card" to={`/listings/${item.id}`}>
            <div className="listing-card-media">
              {item.thumbnailUrl ? (
                <img src={item.thumbnailUrl} alt={item.title} className="listing-thumb" />
              ) : (
                <div className="listing-thumb placeholder">No image</div>
              )}
            </div>
            <div className="listing-card-body">
              <div className="chip-row">
                <span className="chip">{item.category}</span>
                <span className="chip subdued">{item.condition}</span>
              </div>
              <h3>{item.title}</h3>
              <p>
                {item.ageRange || 'All ages'}
                {item.size ? ` · Size ${item.size}` : ''}
              </p>
              <span className="card-meta">Posted {formatDate(item.createdAt)}</span>
            </div>
          </Link>
        ))}
      </div>

      {!loading && !error && data?.items.length === 0 ? (
        <article className="info-card">
          <h3>No listings matched</h3>
          <p>Try a broader search or clear the category filter.</p>
        </article>
      ) : null}

      {data ? (
        <div className="pagination-row">
          <button
            className="secondary-button"
            type="button"
            disabled={data.page <= 1}
            onClick={() => updateSearch({ page: String(Math.max(1, data.page - 1)) })}
          >
            Previous
          </button>
          <span className="page-count">Page {data.page}</span>
          <button
            className="secondary-button"
            type="button"
            disabled={data.page * data.pageSize >= data.totalCount}
            onClick={() => updateSearch({ page: String(data.page + 1) })}
          >
            Next
          </button>
        </div>
      ) : null}
    </section>
  );
}
