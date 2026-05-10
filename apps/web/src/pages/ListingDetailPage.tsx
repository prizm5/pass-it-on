import { type FormEvent, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, type ListingDetail, type ListingImage } from '../lib/api';
import { loadAuth } from '../lib/auth';

const reportReasons = [
  'InappropriateContent',
  'Spam',
  'Duplicate',
  'ProhibitedItem',
  'SafetyConcern',
  'Other',
] as const;

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(new Date(value));
}

export function ListingDetailPage() {
  const { listingId } = useParams();
  const [listing, setListing] = useState<ListingDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reportReason, setReportReason] = useState<(typeof reportReasons)[number]>('SafetyConcern');
  const [reportDescription, setReportDescription] = useState('');
  const [submitState, setSubmitState] = useState<string | null>(null);
  const [imageActionState, setImageActionState] = useState<string | null>(null);
  const [updatingImages, setUpdatingImages] = useState(false);
  const [newImages, setNewImages] = useState<File[]>([]);
  const auth = loadAuth();
  const isOwner = Boolean(auth && listing && auth.user.id === listing.ownerUserId);

  async function readImageDimensions(file: File) {
    const objectUrl = URL.createObjectURL(file);
    try {
      const dimensions = await new Promise<{ width: number; height: number }>((resolve, reject) => {
        const image = new Image();
        image.onload = () => resolve({ width: image.naturalWidth, height: image.naturalHeight });
        image.onerror = () => reject(new Error('Invalid image file.'));
        image.src = objectUrl;
      });

      return dimensions;
    } finally {
      URL.revokeObjectURL(objectUrl);
    }
  }

  useEffect(() => {
    const currentListingId = listingId;

    if (!currentListingId) {
      setError('Listing not found.');
      setLoading(false);
      return;
    }

    let cancelled = false;

    async function load() {
      try {
        setLoading(true);
        setError(null);
        const payload = await api.getListing(currentListingId!);
        if (!cancelled) {
          setListing(payload);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Unable to load listing.');
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
  }, [listingId]);

  async function submitReport(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!listingId) {
      return;
    }

    try {
      setSubmitState('Submitting report...');
      await api.reportListing({
        listingId,
        reasonCode: reportReason,
        description: reportDescription.trim() || undefined,
      });
      setReportDescription('');
      setSubmitState('Report submitted. An admin will review it.');
    } catch (err) {
      setSubmitState(err instanceof Error ? err.message : 'Unable to submit report.');
    }
  }

  async function reorderImage(imageId: string, direction: 'up' | 'down') {
    if (!listingId || !listing || updatingImages) {
      return;
    }

    const currentImages = [...listing.images].sort((a, b) => a.sortOrder - b.sortOrder);
    const currentIndex = currentImages.findIndex((image) => image.id === imageId);
    if (currentIndex < 0) {
      return;
    }

    const targetIndex = direction === 'up' ? currentIndex - 1 : currentIndex + 1;
    if (targetIndex < 0 || targetIndex >= currentImages.length) {
      return;
    }

    [currentImages[currentIndex], currentImages[targetIndex]] = [currentImages[targetIndex], currentImages[currentIndex]];

    const optimisticImages: ListingImage[] = currentImages.map((image, index) => ({
      ...image,
      sortOrder: index,
    }));

    const previousImages = listing.images;
    setListing({ ...listing, images: optimisticImages });

    try {
      setUpdatingImages(true);
      setImageActionState('Saving image order...');
      const reordered = await api.reorderListingImages(listingId, optimisticImages.map((image) => image.id));
      setListing((current) => current ? { ...current, images: [...reordered].sort((a, b) => a.sortOrder - b.sortOrder) } : current);
      setImageActionState('Image order updated.');
    } catch (err) {
      setListing((current) => current ? { ...current, images: previousImages } : current);
      setImageActionState(err instanceof Error ? err.message : 'Unable to reorder images.');
    } finally {
      setUpdatingImages(false);
    }
  }

  async function deleteImage(imageId: string) {
    if (!listingId || !listing || updatingImages) {
      return;
    }

    const previousImages = listing.images;
    setListing({ ...listing, images: listing.images.filter((image) => image.id !== imageId) });

    try {
      setUpdatingImages(true);
      setImageActionState('Deleting image...');
      await api.deleteListingImage(listingId, imageId);

      const remainingIds = previousImages
        .filter((image) => image.id !== imageId)
        .sort((a, b) => a.sortOrder - b.sortOrder)
        .map((image) => image.id);

      if (remainingIds.length > 0) {
        const reordered = await api.reorderListingImages(listingId, remainingIds);
        setListing((current) => current ? { ...current, images: [...reordered].sort((a, b) => a.sortOrder - b.sortOrder) } : current);
      } else {
        setListing((current) => current ? { ...current, images: [] } : current);
      }

      setImageActionState('Image deleted.');
    } catch (err) {
      setListing((current) => current ? { ...current, images: previousImages } : current);
      setImageActionState(err instanceof Error ? err.message : 'Unable to delete image.');
    } finally {
      setUpdatingImages(false);
    }
  }

  async function uploadNewImages(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!listingId || !listing || newImages.length === 0 || updatingImages) {
      return;
    }

    try {
      setUpdatingImages(true);

      const nextImages = [...listing.images];
      const startingSortOrder = nextImages.length;

      for (let index = 0; index < newImages.length; index += 1) {
        const file = newImages[index];
        setImageActionState(`Uploading image ${index + 1} of ${newImages.length}...`);

        const upload = await api.requestListingImageUploadUrl(listingId, {
          fileName: file.name,
          contentType: file.type,
          fileSizeBytes: file.size,
        });

        const uploadResponse = await fetch(upload.uploadUrl, {
          method: 'PUT',
          headers: {
            'Content-Type': file.type,
          },
          body: file,
        });

        if (!uploadResponse.ok) {
          throw new Error(`Image upload failed (${uploadResponse.status}).`);
        }

        const dimensions = await readImageDimensions(file);
        const attached = await api.attachListingImage(listingId, {
          storageKey: upload.storageKey,
          width: dimensions.width,
          height: dimensions.height,
          sortOrder: startingSortOrder + index,
        });

        nextImages.push(attached);
      }

      setListing((current) => current ? { ...current, images: [...nextImages].sort((a, b) => a.sortOrder - b.sortOrder) } : current);
      setNewImages([]);
      setImageActionState('Photos uploaded.');
    } catch (err) {
      setImageActionState(err instanceof Error ? err.message : 'Unable to upload photos.');
    } finally {
      setUpdatingImages(false);
    }
  }

  if (loading) {
    return <section className="page-stack"><p className="status-banner">Loading listing...</p></section>;
  }

  if (error || !listing) {
    return (
      <section className="page-stack">
        <p className="status-banner error">{error ?? 'Listing not found.'}</p>
        <Link className="ghost-button" to="/listings">Back to listings</Link>
      </section>
    );
  }

  return (
    <section className="page-stack">
      <header className="section-heading">
        <p className="eyebrow">Listing detail</p>
        <h2>{listing.title}</h2>
        <p className="section-copy">
          Posted by {listing.ownerDisplayName || 'Community member'} on {formatDate(listing.createdAt)}.
        </p>
      </header>

      <div className="detail-grid">
        <article className="hero-card listing-detail-card">
          <div className="listing-gallery">
            {listing.images.length > 0 ? (
              [...listing.images]
                .sort((a, b) => a.sortOrder - b.sortOrder)
                .map((image, index, ordered) => (
                  <article key={image.id} className="stack-item">
                    <img className="listing-image" src={image.publicUrl} alt={listing.title} />
                    {isOwner ? (
                      <div className="listing-row-actions">
                        <button
                          className="secondary-button"
                          type="button"
                          onClick={() => reorderImage(image.id, 'up')}
                          disabled={updatingImages || index === 0}
                        >
                          Move up
                        </button>
                        <button
                          className="secondary-button"
                          type="button"
                          onClick={() => reorderImage(image.id, 'down')}
                          disabled={updatingImages || index === ordered.length - 1}
                        >
                          Move down
                        </button>
                        <button
                          className="secondary-button danger"
                          type="button"
                          onClick={() => deleteImage(image.id)}
                          disabled={updatingImages}
                        >
                          Delete
                        </button>
                      </div>
                    ) : null}
                  </article>
                ))
            ) : (
              <div className="listing-image placeholder">No photos uploaded yet.</div>
            )}
          </div>
          {isOwner ? (
            <form className="form-stack" onSubmit={uploadNewImages}>
              <label>
                <span>Add more photos</span>
                <input
                  type="file"
                  accept="image/jpeg,image/png"
                  multiple
                  onChange={(event) => setNewImages(Array.from(event.target.files ?? []))}
                />
              </label>
              <button className="secondary-button" type="submit" disabled={updatingImages || newImages.length === 0}>
                {updatingImages ? 'Uploading...' : `Upload ${newImages.length > 0 ? newImages.length : ''} photo${newImages.length === 1 ? '' : 's'}`.trim()}
              </button>
            </form>
          ) : null}
          {isOwner && imageActionState ? <p className="form-message">{imageActionState}</p> : null}
          <div className="chip-row">
            <span className="chip">{listing.category}</span>
            <span className="chip">{listing.condition}</span>
            {listing.size ? <span className="chip">Size {listing.size}</span> : null}
            {listing.ageRange ? <span className="chip">{listing.ageRange}</span> : null}
          </div>
          <p>{listing.description}</p>
          <div className="info-grid compact">
            <div>
              <span className="info-label">Contact preference</span>
              <strong>{listing.contactPreference}</strong>
            </div>
            <div>
              <span className="info-label">Status</span>
              <strong>{listing.status}</strong>
            </div>
          </div>
        </article>

        <aside className="page-stack detail-sidebar">
          <article className="info-card">
            <h3>Exchange handoff</h3>
            <p>
              This MVP does not include in-app chat. Contact happens outside the platform using the
              poster&apos;s chosen method after you connect.
            </p>
          </article>

          <article className="info-card">
            <h3>Report this listing</h3>
            {auth ? (
              <form className="form-stack" onSubmit={submitReport}>
                <label>
                  <span>Reason</span>
                  <select value={reportReason} onChange={(event) => setReportReason(event.target.value as (typeof reportReasons)[number])}>
                    {reportReasons.map((reason) => (
                      <option key={reason} value={reason}>{reason}</option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>Details</span>
                  <textarea
                    rows={4}
                    value={reportDescription}
                    onChange={(event) => setReportDescription(event.target.value)}
                    placeholder="Optional context for the moderation team"
                  />
                </label>
                <button className="primary-button" type="submit">Submit report</button>
                {submitState ? <p className="form-message">{submitState}</p> : null}
              </form>
            ) : (
              <p>
                Sign in from <Link to="/profile">your profile</Link> to report a listing.
              </p>
            )}
          </article>
        </aside>
      </div>
    </section>
  );
}
