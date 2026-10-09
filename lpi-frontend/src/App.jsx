import { useCallback, useEffect, useMemo, useState } from "react";
import "./App.css";
import DeleteButton from "./components/DeleteButton";

const API = "/api";

async function request(path, options = {}) {
  const response = await fetch(`${API}${path}`, {
    ...options,
    headers: {
      ...(options.body ? { "Content-Type": "application/json" } : {}),
      ...options.headers,
    },
  });

  const text = await response.text();
  let data = null;

  try {
    data = text ? JSON.parse(text) : null;
  } catch {
    data = text;
  }

  if (!response.ok) {
    throw new Error(
      data?.error || data?.title || `Request failed (${response.status})`,
    );
  }

  return data;
}

function dateLabel(value) {
  if (!value) return "Unknown date";

  return new Date(value).toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

function formatDuration(milliseconds) {
  if (milliseconds == null) return "0.00";
  return (milliseconds / 1000).toFixed(2);
}

function App() {
  const [page, setPage] = useState("Overview");
  const [health, setHealth] = useState("checking");
  const [sessions, setSessions] = useState([]);
  const [sessionId, setSessionId] = useState("");
  const [documents, setDocuments] = useState([]);
  const [claimsByDocument, setClaimsByDocument] = useState({});
  const [selectedDocumentId, setSelectedDocumentId] = useState("");
  const [selectedClaimId, setSelectedClaimId] = useState("");
  const [selectedEvidenceId, setSelectedEvidenceId] = useState("");
  const [evidenceDocument, setEvidenceDocument] = useState(null);
  const [startOffset, setStartOffset] = useState(0);
  const [passageLength, setPassageLength] = useState(250);
  const [assessment, setAssessment] = useState(null);
  const [relationships, setRelationships] = useState([]);
  const [researchQuestion, setResearchQuestion] = useState("");
  const [documentTitle, setDocumentTitle] = useState("");
  const [documentContent, setDocumentContent] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const [assessmentStartedAt, setAssessmentStartedAt] = useState(null);
  const [assessmentElapsedMs, setAssessmentElapsedMs] = useState(0);
  const [assessmentDurationMs, setAssessmentDurationMs] = useState(null);
  const [assessmentStatus, setAssessmentStatus] = useState("idle");
  const [assessmentLog, setAssessmentLog] = useState(
    "Waiting for an assessment request.",
  );

  const activeSession = sessions.find((s) => s.id === sessionId);
  const activeDocument = documents.find((d) => d.id === selectedDocumentId);

  const allClaims = useMemo(
    () => Object.values(claimsByDocument).flat(),
    [claimsByDocument],
  );

  const selectedClaim = allClaims.find((claim) => claim.id === selectedClaimId);

  const otherEvidenceDocuments = documents.filter(
    (doc) => doc.id !== selectedClaim?.evidenceDocumentId,
  );

  const reportError = useCallback((err) => {
    setError(err?.message || "Something went wrong.");
    setNotice("");
  }, []);

  function resetAssessmentAfterDelete() {
    setSelectedClaimId("");
    setAssessment(null);
    setRelationships([]);
    setAssessmentStartedAt(null);
    setAssessmentElapsedMs(0);
    setAssessmentDurationMs(null);
    setAssessmentStatus("idle");
    setAssessmentLog("Waiting for an assessment request.");
  }

  useEffect(() => {
    if (assessmentStartedAt === null) return undefined;

    const updateElapsed = () => {
      setAssessmentElapsedMs(performance.now() - assessmentStartedAt);
    };

    updateElapsed();

    const intervalId = window.setInterval(updateElapsed, 100);
    return () => window.clearInterval(intervalId);
  }, [assessmentStartedAt]);

  const loadSessions = useCallback(
    async (preferredId = "") => {
      const data = await request("/research-sessions");
      const list = data || [];

      setSessions(list);

      const nextId =
        (preferredId &&
          list.some((s) => s.id === preferredId) &&
          preferredId) ||
        (sessionId && list.some((s) => s.id === sessionId) && sessionId) ||
        list[0]?.id ||
        "";

      setSessionId(nextId);
      return list;
    },
    [sessionId],
  );

  const loadWorkspace = useCallback(async (id) => {
    if (!id) {
      setDocuments([]);
      setClaimsByDocument({});
      setSelectedDocumentId("");
      setSelectedClaimId("");
      setSelectedEvidenceId("");
      setEvidenceDocument(null);
      setRelationships([]);
      return;
    }

    const docs = await request(`/research-sessions/${id}/evidence-documents`);
    const documentList = docs || [];

    setDocuments(documentList);

    const pairs = await Promise.all(
      documentList.map(async (doc) => {
        try {
          const claims = await request(
            `/research-sessions/${id}/evidence-documents/${doc.id}/claims`,
          );

          return [
            doc.id,
            (claims || []).map((claim) => ({
              ...claim,
              sourceDocumentTitle: doc.title,
            })),
          ];
        } catch {
          return [doc.id, []];
        }
      }),
    );

    setClaimsByDocument(Object.fromEntries(pairs));

    setSelectedDocumentId((current) =>
      documentList.some((doc) => doc.id === current)
        ? current
        : documentList[0]?.id || "",
    );

    setSelectedEvidenceId((current) =>
      documentList.some((doc) => doc.id === current)
        ? current
        : documentList[0]?.id || "",
    );
  }, []);

  const refresh = useCallback(
    async (preferredId = "") => {
      setError("");

      try {
        const data = await loadSessions(preferredId);

        const id =
          (preferredId &&
            data.some((s) => s.id === preferredId) &&
            preferredId) ||
          (sessionId && data.some((s) => s.id === sessionId) && sessionId) ||
          data[0]?.id ||
          "";

        await loadWorkspace(id);
      } catch (err) {
        reportError(err);
      }
    },
    [loadSessions, loadWorkspace, sessionId, reportError],
  );

  useEffect(() => {
    request("/health")
      .then(() => setHealth("online"))
      .catch(() => setHealth("offline"));

    refresh();
  }, []);

  useEffect(() => {
    if (sessionId) {
      loadWorkspace(sessionId).catch(reportError);
    } else {
      setDocuments([]);
      setClaimsByDocument({});
    }
  }, [sessionId, loadWorkspace, reportError]);

  useEffect(() => {
    setAssessment(null);
    setRelationships([]);
    setSelectedClaimId("");
    setAssessmentStartedAt(null);
    setAssessmentElapsedMs(0);
    setAssessmentDurationMs(null);
    setAssessmentStatus("idle");
    setAssessmentLog("Waiting for an assessment request.");
  }, [selectedDocumentId]);

  useEffect(() => {
    setEvidenceDocument(null);

    if (!selectedEvidenceId || !sessionId) return undefined;

    let cancelled = false;

    request(
      `/research-sessions/${sessionId}/evidence-documents/${selectedEvidenceId}`,
    )
      .then((doc) => {
        if (cancelled) return;

        setEvidenceDocument(doc);
        setStartOffset(0);
        setPassageLength(Math.min(250, doc.content?.length || 250));
      })
      .catch((err) => {
        if (!cancelled) reportError(err);
      });

    return () => {
      cancelled = true;
    };
  }, [selectedEvidenceId, sessionId, reportError]);

  // DELETE HANDLERS: these must stay inside App so they can access React state.

  async function handleSessionDeleted(deletedId) {
    try {
      setError("");

      const remainingSessions = await request("/research-sessions");
      const list = remainingSessions || [];

      setSessions(list);

      const nextId = list.some((session) => session.id === sessionId)
        ? sessionId
        : list[0]?.id || "";

      if (deletedId === sessionId) {
        resetAssessmentAfterDelete();
        setSelectedDocumentId("");
        setSelectedEvidenceId("");
        setEvidenceDocument(null);
      }

      setSessionId(nextId);
      await loadWorkspace(nextId);
      setNotice("Research session deleted.");
    } catch (err) {
      reportError(err);
    }
  }

  async function handleDocumentDeleted(deletedId) {
    try {
      setError("");

      if (selectedDocumentId === deletedId) {
        resetAssessmentAfterDelete();
        setSelectedDocumentId("");
      }

      if (selectedEvidenceId === deletedId) {
        setSelectedEvidenceId("");
        setEvidenceDocument(null);
      }

      await loadWorkspace(sessionId);
      setNotice("Evidence document deleted.");
    } catch (err) {
      reportError(err);
    }
  }

  async function handleClaimDeleted(claim) {
    try {
      setError("");

      if (selectedClaimId === claim.id) {
        resetAssessmentAfterDelete();
      }

      await loadWorkspace(sessionId);
      setNotice("Candidate claim deleted.");
    } catch (err) {
      reportError(err);
    }
  }

  async function handleRelationshipDeleted() {
    try {
      setError("");

      if (!selectedClaim) {
        setRelationships([]);
        return;
      }

      const saved = await request(
        `/research-sessions/${sessionId}/claims/${selectedClaim.id}/relationships`,
      );

      setRelationships(saved || []);
      setNotice("Evidence relationship deleted.");
    } catch (err) {
      reportError(err);
    }
  }

  async function createSession(event) {
    event.preventDefault();
    setBusy(true);
    setError("");
    setNotice("");

    try {
      const created = await request("/research-sessions", {
        method: "POST",
        body: JSON.stringify({ researchQuestion }),
      });

      setResearchQuestion("");
      await loadSessions(created.id);
      await loadWorkspace(created.id);
      setPage("Research Sessions");
      setNotice("Research session created.");
    } catch (err) {
      reportError(err);
    } finally {
      setBusy(false);
    }
  }

  async function importDocument(event) {
    event.preventDefault();

    if (!sessionId) {
      setError("Create or select a research session first.");
      return;
    }

    setBusy(true);
    setError("");
    setNotice("");

    try {
      const created = await request(
        `/research-sessions/${sessionId}/evidence-documents`,
        {
          method: "POST",
          body: JSON.stringify({
            title: documentTitle,
            content: documentContent,
          }),
        },
      );

      setDocumentTitle("");
      setDocumentContent("");
      await loadWorkspace(sessionId);
      setSelectedDocumentId(created.id);
      setSelectedEvidenceId(created.id);
      setPage("Evidence Documents");
      setNotice("Evidence document imported.");
    } catch (err) {
      reportError(err);
    } finally {
      setBusy(false);
    }
  }

  async function extractClaims(doc) {
    if (!sessionId || !doc) return;

    setBusy(true);
    setError("");
    setNotice("");

    try {
      const result = await request(
        `/research-sessions/${sessionId}/evidence-documents/${doc.id}/claims/extract`,
        { method: "POST" },
      );

      await loadWorkspace(sessionId);
      setSelectedDocumentId(doc.id);
      setPage("Candidate Claims");
      setNotice(
        `Claim extraction complete: ${result.extractedCount ?? 0} claims found.`,
      );
    } catch (err) {
      reportError(err);
    } finally {
      setBusy(false);
    }
  }

  async function assessEvidence(event) {
    event.preventDefault();

    if (busy) return;

    if (!selectedClaim || !evidenceDocument) {
      setError("Choose a claim and an evidence document first.");
      return;
    }

    if (selectedClaim.evidenceDocumentId === evidenceDocument.id) {
      setError(
        "Choose evidence from a different document than the claim source.",
      );
      return;
    }

    const offset = Number(startOffset);
    const length = Number(passageLength);

    if (
      !Number.isInteger(offset) ||
      offset < 0 ||
      !Number.isInteger(length) ||
      length < 1 ||
      length > 12000
    ) {
      setError("Enter a valid passage offset and length.");
      return;
    }

    const startedAt = performance.now();

    setBusy(true);
    setError("");
    setNotice("");
    setAssessment(null);
    setAssessmentStartedAt(startedAt);
    setAssessmentElapsedMs(0);
    setAssessmentDurationMs(null);
    setAssessmentStatus("running");
    setAssessmentLog(
      "Assessment request sent. Waiting for the local AI response…",
    );

    try {
      const result = await request(
        `/research-sessions/${sessionId}/claims/${selectedClaim.id}/assess`,
        {
          method: "POST",
          body: JSON.stringify({
            evidenceDocumentId: evidenceDocument.id,
            startOffset: offset,
            length,
          }),
        },
      );

      setAssessment(result);
      setAssessmentStatus("completed");
      setAssessmentLog("Assessment response received successfully.");
      setNotice("Local AI assessment completed. Review it before saving.");
    } catch (err) {
      setAssessmentStatus("failed");
      setAssessmentLog(
        `Assessment request failed: ${err?.message || "Unknown error"}`,
      );
      reportError(err);
    } finally {
      const duration = performance.now() - startedAt;

      setAssessmentElapsedMs(duration);
      setAssessmentDurationMs(duration);
      setAssessmentStartedAt(null);
      setBusy(false);
    }
  }

  async function saveRelationship() {
    if (!assessment || !selectedClaim || busy) return;

    setBusy(true);
    setError("");
    setNotice("");

    try {
      await request(
        `/research-sessions/${sessionId}/claims/${selectedClaim.id}/relationships`,
        {
          method: "POST",
          body: JSON.stringify({
            evidenceDocumentId: assessment.evidenceDocumentId,
            relationshipType: assessment.relationshipType,
            evidenceText: assessment.evidenceText,
            startOffset: assessment.startOffset,
            assessmentMethod: "LocalAI",
            explanation: assessment.explanation,
          }),
        },
      );

      const saved = await request(
        `/research-sessions/${sessionId}/claims/${selectedClaim.id}/relationships`,
      );

      setRelationships(saved || []);
      setAssessment({ ...assessment, saved: true });
      setNotice("Assessment saved as an evidence relationship.");
    } catch (err) {
      reportError(err);
    } finally {
      setBusy(false);
    }
  }

  async function loadRelationships(claim) {
    if (!claim) {
      setSelectedClaimId("");
      setRelationships([]);
      setAssessment(null);
      return;
    }

    setSelectedClaimId(claim.id);
    setAssessment(null);
    setAssessmentDurationMs(null);
    setAssessmentElapsedMs(0);
    setAssessmentStartedAt(null);
    setAssessmentStatus("idle");
    setAssessmentLog("Claim selected. Ready to assess evidence.");
    setError("");

    try {
      const saved = await request(
        `/research-sessions/${sessionId}/claims/${claim.id}/relationships`,
      );

      setRelationships(saved || []);
    } catch (err) {
      reportError(err);
    }
  }

  const nav = [
    ["Overview", "◫"],
    ["Research Sessions", "◎"],
    ["Evidence Documents", "▤"],
    ["Candidate Claims", "◇"],
    ["Evidence Analysis", "⌁"],
  ];

  const displayedDuration =
    assessmentStartedAt !== null ? assessmentElapsedMs : assessmentDurationMs;

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-mark">
            L<span>✦</span>
          </div>
          <div>
            <strong>LUNAR PROBE</strong>
            <small>INTELLIGENCE</small>
          </div>
        </div>

        <div className="workspace-label">WORKSPACE</div>

        <nav>
          {nav.map(([label, icon]) => (
            <button
              key={label}
              className={`nav-link ${page === label ? "active" : ""}`}
              onClick={() => {
                setPage(label);
                setError("");
                setNotice("");
              }}
            >
              <span className="nav-icon">{icon}</span>
              {label}
              {label === "Evidence Documents" && documents.length > 0 && (
                <span className="nav-count">{documents.length}</span>
              )}
            </button>
          ))}
        </nav>

        <div className="sidebar-bottom">
          <div className="local-card">
            <span
              className={`status-dot ${health === "online" ? "good" : ""}`}
            />
            <div>
              <strong>Local environment</strong>
              <small>
                {health === "online"
                  ? "API connected"
                  : health === "checking"
                    ? "Checking API…"
                    : "API unavailable"}
              </small>
            </div>
          </div>

          <div className="privacy-note">
            ⌁ &nbsp; Local-first research
            <br />
            <span>Your evidence stays on your machine.</span>
          </div>
        </div>
      </aside>

      <main className="main">
        <header className="topbar">
          <div className="breadcrumb">
            LPI <span>/</span> {page}
          </div>

          <div className="topbar-right">
            <span className={`api-pill ${health}`}>
              {health === "online"
                ? "● API ONLINE"
                : health === "checking"
                  ? "◌ CONNECTING"
                  : "● API OFFLINE"}
            </span>

            <button
              className="icon-button"
              title="Refresh workspace"
              onClick={() => refresh()}
            >
              ↻
            </button>
          </div>
        </header>

        <div className="content">
          {error && (
            <div className="alert error">
              <strong>Something went wrong</strong>
              <span>{error}</span>
              <button onClick={() => setError("")}>×</button>
            </div>
          )}

          {notice && (
            <div className="alert success">
              <span>✓</span>
              {notice}
              <button onClick={() => setNotice("")}>×</button>
            </div>
          )}

          <section className="page-heading">
            <div>
              <div className="eyebrow">EVIDENCE INTELLIGENCE PLATFORM</div>
              <h1>{page}</h1>
              <p className="muted">
                {page === "Overview" &&
                  "Organize research, examine evidence, and trace every assessment to its source."}
                {page === "Research Sessions" &&
                  "Create and manage focused investigations."}
                {page === "Evidence Documents" &&
                  "Import source text and inspect its provenance."}
                {page === "Candidate Claims" &&
                  "Extract sentence-level claims with source offsets."}
                {page === "Evidence Analysis" &&
                  "Assess a claim against a passage from another document."}
              </p>
            </div>

            <div className="session-select">
              <label>ACTIVE INVESTIGATION</label>
              <select
                value={sessionId}
                onChange={(e) => setSessionId(e.target.value)}
              >
                <option value="">Select a session</option>
                {sessions.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.researchQuestion}
                  </option>
                ))}
              </select>
            </div>
          </section>

          {page === "Overview" && (
            <>
              <div className="hero">
                <div className="hero-copy">
                  <div className="hero-tag">
                    <span /> LOCAL RESEARCH WORKSPACE
                  </div>

                  <h2>
                    Follow the evidence.
                    <br />
                    <em>Not the assumption.</em>
                  </h2>

                  <p>
                    Build a traceable chain from research questions to source
                    passages and evidence assessments.
                  </p>

                  <button
                    className="button light"
                    onClick={() => setPage("Research Sessions")}
                  >
                    Open workspace <span>→</span>
                  </button>
                </div>

                <div className="hero-art" aria-hidden="true">
                  <div className="orbit orbit-one" />
                  <div className="orbit orbit-two" />
                  <div className="planet">
                    L<span>✦</span>
                  </div>
                  <div className="orbit-dot dot-one" />
                  <div className="orbit-dot dot-two" />
                  <div className="orbit-dot dot-three" />
                </div>
              </div>

              <div className="stats-grid">
                <Stat
                  label="Research sessions"
                  value={sessions.length}
                  icon="◎"
                />
                <Stat
                  label="Evidence documents"
                  value={documents.length}
                  icon="▤"
                />
                <Stat
                  label="Candidate claims"
                  value={allClaims.length}
                  icon="◇"
                />
                <Stat
                  label="Saved relationships"
                  value={relationships.length}
                  icon="⌁"
                />
              </div>

              <div className="section-head">
                <div>
                  <h2>Workspace overview</h2>
                  <p className="muted">
                    Continue an investigation or begin a new one.
                  </p>
                </div>

                <button
                  className="button primary"
                  onClick={() => setPage("Research Sessions")}
                >
                  ＋ New investigation
                </button>
              </div>

              <div className="overview-grid">
                <div className="panel">
                  <div className="panel-title">
                    <h3>Recent investigations</h3>
                    <button
                      className="text-button"
                      onClick={() => setPage("Research Sessions")}
                    >
                      View all →
                    </button>
                  </div>

                  {sessions.slice(0, 4).map((s) => (
                    <div className="deletable-row" key={s.id}>
                      <button
                        type="button"
                        className="session-row"
                        onClick={() => {
                          setSessionId(s.id);
                          setPage("Evidence Documents");
                        }}
                      >
                        <span className="row-symbol">◎</span>
                        <span className="row-main">
                          <strong>{s.researchQuestion}</strong>
                          <small>Created {dateLabel(s.createdAtUtc)}</small>
                        </span>
                        <span className="row-arrow">↗</span>
                      </button>

                      <DeleteButton
                        endpoint={`${API}/research-sessions/${s.id}`}
                        itemName="this research session"
                        onDeleted={() => handleSessionDeleted(s.id)}
                      />
                    </div>
                  ))}

                  {sessions.length === 0 && (
                    <Empty text="No research sessions yet. Create your first investigation below." />
                  )}
                </div>

                <div className="panel quick-panel">
                  <div className="panel-title">
                    <h3>Research workflow</h3>
                    <span className="small-label">4 STEPS</span>
                  </div>

                  {[
                    [
                      "01",
                      "Define the question",
                      "Create a focused research session.",
                    ],
                    [
                      "02",
                      "Import source material",
                      "Add documents as evidence.",
                    ],
                    [
                      "03",
                      "Extract candidate claims",
                      "Find traceable sentences in a source.",
                    ],
                    [
                      "04",
                      "Assess and compare",
                      "Review evidence with local AI.",
                    ],
                  ].map(([n, title, desc]) => (
                    <div className="workflow-row" key={n}>
                      <span>{n}</span>
                      <div>
                        <strong>{title}</strong>
                        <small>{desc}</small>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </>
          )}

          {page === "Research Sessions" && (
            <div className="two-column">
              <div className="panel">
                <div className="panel-title">
                  <h3>Create an investigation</h3>
                  <span className="small-label">NEW SESSION</span>
                </div>

                <form className="form-stack" onSubmit={createSession}>
                  <label>Research question</label>
                  <textarea
                    required
                    maxLength={2000}
                    rows={5}
                    value={researchQuestion}
                    onChange={(e) => setResearchQuestion(e.target.value)}
                    placeholder="What question do you want to investigate?"
                  />
                  <div className="form-hint">
                    {researchQuestion.length}/2000 characters
                  </div>
                  <button className="button primary" disabled={busy}>
                    {busy ? "Working…" : "＋ Create session"}
                  </button>
                </form>
              </div>

              <div className="panel">
                <div className="panel-title">
                  <h3>All investigations</h3>
                  <span className="small-label">{sessions.length} TOTAL</span>
                </div>

                {sessions.map((s) => (
                  <div className="deletable-row" key={s.id}>
                    <button
                      type="button"
                      className={`session-row ${sessionId === s.id ? "chosen" : ""}`}
                      onClick={() => setSessionId(s.id)}
                    >
                      <span className="row-symbol">◎</span>
                      <span className="row-main">
                        <strong>{s.researchQuestion}</strong>
                        <small>
                          {dateLabel(s.createdAtUtc)} · {s.status || "Pending"}
                        </small>
                      </span>
                      <span className="row-arrow">→</span>
                    </button>

                    <DeleteButton
                      endpoint={`${API}/research-sessions/${s.id}`}
                      itemName="this research session"
                      onDeleted={() => handleSessionDeleted(s.id)}
                    />
                  </div>
                ))}

                {sessions.length === 0 && (
                  <Empty text="Your investigations will appear here." />
                )}
              </div>
            </div>
          )}

          {page === "Evidence Documents" && (
            <div className="two-column evidence-layout">
              <div className="panel">
                <div className="panel-title">
                  <h3>Import source text</h3>
                  <span className="small-label">TEXT IMPORT</span>
                </div>

                <form className="form-stack" onSubmit={importDocument}>
                  <label>Document title</label>
                  <input
                    required
                    maxLength={300}
                    value={documentTitle}
                    onChange={(e) => setDocumentTitle(e.target.value)}
                    placeholder="e.g. Research paper excerpt"
                  />

                  <label>Document content</label>
                  <textarea
                    required
                    rows={9}
                    maxLength={1000000}
                    value={documentContent}
                    onChange={(e) => setDocumentContent(e.target.value)}
                    placeholder="Paste source text here. Source offsets will be preserved."
                  />

                  <div className="form-hint">
                    {documentContent.length.toLocaleString()} / 1,000,000
                    characters
                  </div>

                  <button
                    className="button primary"
                    disabled={busy || !sessionId}
                  >
                    {busy ? "Importing…" : "＋ Import document"}
                  </button>

                  {!sessionId && (
                    <p className="form-hint">
                      Select or create a research session first.
                    </p>
                  )}
                </form>

                <div className="divider" />

                <div className="panel-title">
                  <h3>Source library</h3>
                  <span className="small-label">
                    {documents.length} DOCUMENTS
                  </span>
                </div>

                {documents.map((doc) => (
                  <div className="deletable-row" key={doc.id}>
                    <button
                      type="button"
                      className={`document-row ${selectedDocumentId === doc.id ? "chosen" : ""}`}
                      onClick={() => setSelectedDocumentId(doc.id)}
                    >
                      <span className="doc-icon">▤</span>
                      <span className="row-main">
                        <strong>{doc.title}</strong>
                        <small>
                          {doc.characterCount?.toLocaleString() ?? "—"}{" "}
                          characters · {dateLabel(doc.importedAtUtc)}
                        </small>
                      </span>
                      <span className="row-arrow">→</span>
                    </button>

                    <DeleteButton
                      endpoint={`${API}/research-sessions/${sessionId}/evidence-documents/${doc.id}`}
                      itemName="this evidence document"
                      onDeleted={() => handleDocumentDeleted(doc.id)}
                    />
                  </div>
                ))}

                {documents.length === 0 && (
                  <Empty text="No source documents in this session yet." />
                )}
              </div>

              <div className="panel document-viewer">
                <div className="panel-title">
                  <h3>Source viewer</h3>
                  {activeDocument && (
                    <span className="small-label">SOURCE TEXT</span>
                  )}
                </div>

                {activeDocument ? (
                  <>
                    <h2 className="document-title">{activeDocument.title}</h2>
                    <div className="source-meta">
                      ID <code>{activeDocument.id}</code>
                    </div>
                    <pre className="source-text">
                      {evidenceDocument?.id === activeDocument.id
                        ? evidenceDocument.content
                        : "Select this document as the passage source to load its full text."}
                    </pre>
                    <button
                      className="button primary"
                      disabled={busy}
                      onClick={() => extractClaims(activeDocument)}
                    >
                      {busy ? "Processing…" : "◇ Extract candidate claims"}
                    </button>
                    <p className="form-hint">
                      Extraction is performed by the backend. Existing extracted
                      claims cannot currently be re-extracted through this
                      endpoint.
                    </p>
                  </>
                ) : (
                  <Empty text="Select a document to inspect its source content." />
                )}
              </div>
            </div>
          )}

          {page === "Candidate Claims" && (
            <div className="panel">
              <div className="panel-title">
                <h3>Extracted claims</h3>
                <span className="small-label">{allClaims.length} CLAIMS</span>
              </div>

              <div className="filter-row">
                <label>Source document</label>
                <select
                  value={selectedDocumentId}
                  onChange={(e) => setSelectedDocumentId(e.target.value)}
                >
                  <option value="">All / select a document</option>
                  {documents.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.title}
                    </option>
                  ))}
                </select>
              </div>

              {(selectedDocumentId
                ? claimsByDocument[selectedDocumentId] || []
                : allClaims
              ).map((claim) => (
                <div className="claim-card" key={claim.id}>
                  <div className="claim-index">
                    CLAIM · OFFSET {claim.startOffset}
                  </div>
                  <p>{claim.claimText}</p>
                  <div className="claim-footer">
                    <span>{claim.sourceDocumentTitle}</span>
                    <span>{claim.length} chars</span>
                    <button
                      className="button small primary"
                      onClick={() => {
                        loadRelationships(claim);
                        setPage("Evidence Analysis");
                      }}
                    >
                      Assess evidence →
                    </button>
                    <DeleteButton
                      endpoint={`${API}/research-sessions/${sessionId}/evidence-documents/${claim.evidenceDocumentId}/claims/${claim.id}`}
                      itemName="this candidate claim"
                      onDeleted={() => handleClaimDeleted(claim)}
                    />
                  </div>
                </div>
              ))}

              {allClaims.length === 0 && (
                <Empty text="No claims extracted yet. Open Evidence Documents and extract claims from a source." />
              )}
            </div>
          )}

          {page === "Evidence Analysis" && (
            <div className="analysis-grid">
              <div className="panel">
                <div className="panel-title">
                  <h3>Assessment setup</h3>
                  <span className="small-label">LOCAL AI</span>
                </div>

                <form className="form-stack" onSubmit={assessEvidence}>
                  <label>Candidate claim</label>
                  <select
                    value={selectedClaimId}
                    onChange={(e) =>
                      loadRelationships(
                        allClaims.find((c) => c.id === e.target.value),
                      )
                    }
                  >
                    <option value="">Choose a claim</option>
                    {allClaims.map((claim) => (
                      <option key={claim.id} value={claim.id}>
                        {claim.claimText.slice(0, 110)}
                        {claim.claimText.length > 110 ? "…" : ""}
                      </option>
                    ))}
                  </select>

                  {selectedClaim && (
                    <div className="selected-claim">
                      <span className="small-label">
                        CLAIM SOURCE · {selectedClaim.sourceDocumentTitle}
                      </span>
                      <p>{selectedClaim.claimText}</p>
                    </div>
                  )}

                  <label>
                    Evidence document (must differ from claim source)
                  </label>
                  <select
                    value={selectedEvidenceId}
                    onChange={(e) => setSelectedEvidenceId(e.target.value)}
                  >
                    <option value="">Choose a source</option>
                    {otherEvidenceDocuments.map((doc) => (
                      <option key={doc.id} value={doc.id}>
                        {doc.title}
                      </option>
                    ))}
                  </select>

                  {evidenceDocument && (
                    <>
                      <label>Passage start offset</label>
                      <input
                        type="number"
                        min="0"
                        max={Math.max(0, evidenceDocument.content.length - 1)}
                        value={startOffset}
                        onChange={(e) => setStartOffset(e.target.value)}
                        required
                      />

                      <label>Passage length</label>
                      <input
                        type="number"
                        min="1"
                        max="12000"
                        value={passageLength}
                        onChange={(e) => setPassageLength(e.target.value)}
                        required
                      />

                      <div className="passage-preview">
                        <div className="small-label">
                          SELECTED PASSAGE PREVIEW
                        </div>
                        <p>
                          {evidenceDocument.content.slice(
                            Number(startOffset),
                            Number(startOffset) + Number(passageLength),
                          ) || "No text at this offset."}
                        </p>
                        <small>
                          Offset {startOffset} · {passageLength} characters
                          requested
                        </small>
                      </div>
                    </>
                  )}

                  <button
                    className="button primary full"
                    disabled={
                      busy ||
                      !selectedClaim ||
                      !evidenceDocument ||
                      selectedClaim.evidenceDocumentId === selectedEvidenceId
                    }
                  >
                    {assessmentStatus === "running"
                      ? "Assessing with local AI…"
                      : "✦ Assess evidence"}
                  </button>

                  {otherEvidenceDocuments.length === 0 && (
                    <p className="form-hint">
                      Import at least two documents to assess evidence from a
                      separate source.
                    </p>
                  )}

                  <p className="form-hint">
                    The assessment uses your local Ollama service through the
                    API. It does not establish factual truth or source
                    reliability.
                  </p>
                </form>
              </div>

              <div className="panel results-panel">
                <div className="panel-title">
                  <h3>Assessment result</h3>
                  <span className="small-label">
                    {assessment?.saved
                      ? "SAVED"
                      : assessment
                        ? "UNSAVED"
                        : "AWAITING ANALYSIS"}
                  </span>
                </div>

                <div
                  className={`assessment-timer ${assessmentStatus}`}
                  aria-live="polite"
                >
                  <div className="assessment-timer-header">
                    <div className="timer-info">
                      <span className="timer-icon">
                        {assessmentStatus === "running"
                          ? "◷"
                          : assessmentStatus === "completed"
                            ? "✓"
                            : assessmentStatus === "failed"
                              ? "!"
                              : "◴"}
                      </span>

                      <div>
                        <strong>
                          {assessmentStatus === "running"
                            ? "Assessment in progress"
                            : assessmentStatus === "completed"
                              ? "Assessment completed"
                              : assessmentStatus === "failed"
                                ? "Assessment failed"
                                : "AI Assessment Time"}
                        </strong>

                        <small>
                          {assessmentStatus === "running"
                            ? "Waiting for the local AI response…"
                            : assessmentStatus === "completed"
                              ? "Total time for the last assessment"
                              : assessmentStatus === "failed"
                                ? "Time elapsed before the request failed"
                                : "Time will appear when you assess evidence"}
                        </small>
                      </div>
                    </div>

                    <strong className="timer-value" aria-live="off">
                      {displayedDuration === null
                        ? "—"
                        : formatDuration(displayedDuration)}
                      {displayedDuration !== null && <span> sec</span>}
                    </strong>
                  </div>

                  <div
                    className="assessment-progress"
                    role="progressbar"
                    aria-label="AI assessment progress"
                    aria-valuemin={0}
                    aria-valuemax={100}
                    aria-valuenow={
                      assessmentStatus === "completed" ? 100 : undefined
                    }
                  >
                    <div className="assessment-progress-fill" />
                  </div>

                  <p className="assessment-log">
                    <span className="log-indicator" />
                    <span className="log-prefix">LOG</span>
                    <span>{assessmentLog}</span>
                  </p>
                </div>

                {assessment ? (
                  <>
                    <div className="result-tags">
                      <span
                        className={`result-tag ${assessment.relationshipType?.toLowerCase()}`}
                      >
                        {assessment.relationshipType}
                      </span>
                      <span className="result-tag">
                        {assessment.evidenceStrength} evidence
                      </span>
                      <span className="result-tag">
                        Uncertainty: {assessment.uncertainty}
                      </span>
                    </div>

                    <div className="result-block">
                      <div className="small-label">EVIDENCE PASSAGE</div>
                      <blockquote>{assessment.evidenceText}</blockquote>
                      <small>
                        {assessment.evidenceDocumentTitle} · offset{" "}
                        {assessment.startOffset} · {assessment.length}{" "}
                        characters
                      </small>
                    </div>

                    <div className="result-block">
                      <div className="small-label">
                        EXPLANATION & LIMITATIONS
                      </div>
                      <p>{assessment.explanation}</p>
                    </div>

                    <div className="provenance-note">
                      ⌁{" "}
                      <span>
                        <strong>Provenance preserved</strong>
                        <br />
                        This result refers to a specific passage and its source
                        offset.
                      </span>
                    </div>

                    <button
                      className="button primary full"
                      disabled={busy || assessment.saved}
                      onClick={saveRelationship}
                    >
                      {assessment.saved
                        ? "✓ Relationship saved"
                        : busy
                          ? "Saving…"
                          : "Save evidence relationship"}
                    </button>
                  </>
                ) : (
                  <Empty text="Choose a claim and a passage from another document, then run an assessment to see the result here." />
                )}

                {relationships.length > 0 && selectedClaim && (
                  <div className="saved-list">
                    <h4>Saved relationships · {relationships.length}</h4>

                    {relationships.map((rel) => (
                      <div className="saved-row" key={rel.id}>
                        <span
                          className={`mini-dot ${rel.relationshipType?.toLowerCase()}`}
                        />

                        <div>
                          <strong>{rel.relationshipType}</strong>
                          <small>
                            {rel.assessmentMethod} · offset {rel.startOffset} ·{" "}
                            {dateLabel(rel.createdAtUtc)}
                          </small>
                        </div>

                        <DeleteButton
                          endpoint={`${API}/research-sessions/${sessionId}/claims/${selectedClaim.id}/relationships/${rel.id}`}
                          itemName="this evidence relationship"
                          onDeleted={handleRelationshipDeleted}
                        />
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}

          <footer className="footer">
            <span>LUNAR PROBE INTELLIGENCE</span>
            <span>LOCAL-FIRST · EVIDENCE-TRACEABLE · HUMAN-REVIEWED</span>
          </footer>
        </div>
      </main>
    </div>
  );
}

function Stat({ label, value, icon }) {
  return (
    <div className="stat-card">
      <div className="stat-top">
        <span>{label}</span>
        <span className="stat-icon">{icon}</span>
      </div>
      <strong>{value}</strong>
      <small>Current investigation</small>
    </div>
  );
}

function Empty({ text }) {
  return (
    <div className="empty-state">
      <span>◌</span>
      <p>{text}</p>
    </div>
  );
}

export default App;
