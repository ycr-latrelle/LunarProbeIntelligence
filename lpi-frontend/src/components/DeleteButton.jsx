import { useState } from "react";

export default function DeleteButton({
  endpoint,
  itemName = "this item",
  onDeleted,
  className = "",
}) {
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState("");

  async function handleDelete() {
    const confirmed = window.confirm(
      `Are you sure you want to delete ${itemName}?\n\nThis action cannot be undone.`,
    );

    if (!confirmed || deleting) return;

    setDeleting(true);
    setError("");

    try {
      const response = await fetch(endpoint, {
        method: "DELETE",
        headers: {
          Accept: "application/json",
        },
      });

      if (!response.ok) {
        let message = `Deletion failed (${response.status}).`;

        try {
          const body = await response.json();
          message = body.error || body.message || message;
        } catch {
          // The server returned no JSON error body.
        }

        throw new Error(message);
      }

      // Update the parent list only after server confirmation.
      await onDeleted?.();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "An unexpected error occurred.",
      );
    } finally {
      setDeleting(false);
    }
  }

  return (
    <span className="delete-action">
      <button
        type="button"
        className={`delete-button ${className}`.trim()}
        onClick={handleDelete}
        disabled={deleting}
        aria-label={`Delete ${itemName}`}
      >
        {deleting ? "Deleting..." : "Delete"}
      </button>

      {error && (
        <span className="delete-error" role="alert">
          {error}
        </span>
      )}
    </span>
  );
}
