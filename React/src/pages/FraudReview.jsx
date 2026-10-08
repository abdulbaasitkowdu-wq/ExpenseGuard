import { useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getFraudFlags, resolveFraudFlag, reviewFraudFlag } from '../services/api';
import { useAuth } from '../auth/AuthContext';
import { AccessGate, MutationError, QueryState, StatePanel } from '../components/RequestState';
import { Pagination, StatusBadge } from '../components/Shared';

export function FraudQueue() {
  const [filters, setFilters] = useState({ status: '', severity: '', claimId: '', page: 1, pageSize: 10 });
  const query = useQuery({
    queryKey: ['fraud-flags', filters],
    queryFn: () => getFraudFlags({
      ...filters,
      status: filters.status || undefined,
      severity: filters.severity || undefined,
      claimId: filters.claimId || undefined,
    }).then(response => response.data),
  });
  return (
    <AccessGate roles={['Admin', 'FraudAnalyst', 'Finance', 'Auditor']}>
      <div className="page-content feature-page">
        <header className="page-heading"><div><h2>Fraud review queue</h2><p>Review deterministic flags and preserve an auditable decision.</p></div></header>
        <div className="table-wrapper">
          <div className="filter-bar">
            <select aria-label="Status filter" value={filters.status} onChange={e => setFilters({ ...filters, status: e.target.value, page: 1 })}>
              <option value="">All statuses</option><option value="open">Open</option><option value="under_review">Under review</option><option value="confirmed">Confirmed</option><option value="dismissed">Dismissed</option><option value="resolved">Resolved</option>
            </select>
            <select aria-label="Severity filter" value={filters.severity} onChange={e => setFilters({ ...filters, severity: e.target.value, page: 1 })}>
              <option value="">All severities</option><option value="low">Low</option><option value="medium">Medium</option><option value="high">High</option><option value="critical">Critical</option>
            </select>
            <input aria-label="Claim ID filter" type="number" min="1" placeholder="Claim ID" value={filters.claimId} onChange={e => setFilters({ ...filters, claimId: e.target.value, page: 1 })} />
          </div>
          <QueryState query={query} empty={query.data?.items?.length === 0}>
            <table><thead><tr><th>Flag</th><th>Claim</th><th>Rule</th><th>Risk</th><th>Status</th><th>Created</th></tr></thead>
              <tbody>{query.data?.items?.map(flag => (
                <tr key={flag.fraudFlagId}>
                  <td><Link to={`/fraud/${flag.fraudFlagId}`} state={{ flag }}>#{flag.fraudFlagId}</Link></td>
                  <td>#{flag.expenseClaimId}</td><td>{flag.ruleCode}</td>
                  <td><span className={`badge ${flag.severity === 'high' || flag.severity === 'critical' ? 'failed' : 'waiting'}`}>{flag.severity} · {flag.riskScore}</span></td>
                  <td><StatusBadge status={flag.status} /></td><td>{new Date(flag.createdAt).toLocaleString()}</td>
                </tr>
              ))}</tbody>
            </table>
            {(query.data?.total ?? 0) > filters.pageSize && <Pagination page={filters.page} totalPages={Math.ceil(query.data.total / filters.pageSize)} onPageChange={page => setFilters({ ...filters, page })} />}
          </QueryState>
        </div>
      </div>
    </AccessGate>
  );
}

export function FraudDetail() {
  const { id } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const auth = useAuth();
  const canDecide = auth.hasAnyRole('Admin', 'FraudAnalyst');
  const [status, setStatus] = useState('under_review');
  const [note, setNote] = useState('');
  const [validation, setValidation] = useState('');
  const client = useQueryClient();
  const query = useQuery({
    queryKey: ['fraud-flag', id],
    queryFn: () => getFraudFlags({ page: 1, pageSize: 100 }).then(response => response.data.items.find(item => String(item.fraudFlagId) === id) ?? null),
    initialData: location.state?.flag,
  });
  const decision = useMutation({
    mutationFn: () => status === 'resolved'
      ? resolveFraudFlag(id, { resolutionNote: note.trim() })
      : reviewFraudFlag(id, { status, note: note.trim() || null }),
    onSuccess: response => {
      client.setQueryData(['fraud-flag', id], response.data);
      client.invalidateQueries({ queryKey: ['fraud-flags'] });
      setNote('');
    },
  });
  function submit(event) {
    event.preventDefault();
    if (status === 'resolved' && note.trim().length < 3) {
      setValidation('A resolution note of at least 3 characters is required.');
      return;
    }
    setValidation('');
    decision.mutate();
  }

  return (
    <AccessGate roles={['Admin', 'FraudAnalyst', 'Finance', 'Auditor']}>
      <div className="page-content feature-page">
        <button className="btn btn-ghost" onClick={() => navigate('/fraud')}>← Queue</button>
        <QueryState query={query} empty={query.data === null}>
          {query.data ? <FlagDetails flag={query.data} canDecide={canDecide} status={status} setStatus={setStatus} note={note} setNote={setNote} validation={validation} decision={decision} submit={submit} /> : null}
        </QueryState>
      </div>
    </AccessGate>
  );
}

function FlagDetails({ flag, canDecide, status, setStatus, note, setNote, validation, decision, submit }) {
  let evidence = flag.evidenceJson;
  try { evidence = JSON.stringify(JSON.parse(flag.evidenceJson), null, 2); } catch { /* opaque evidence is safe to display */ }
  return (
    <>
      <header className="page-heading"><div><h2>Fraud flag #{flag.fraudFlagId}</h2><p>Claim #{flag.expenseClaimId} · {flag.ruleCode}</p></div><StatusBadge status={flag.status} /></header>
      <div className="detail-grid">
        <section className="card"><h3>Assessment</h3><dl className="detail-list"><dt>Severity</dt><dd>{flag.severity}</dd><dt>Risk score</dt><dd>{flag.riskScore}</dd><dt>Source</dt><dd>{flag.source}</dd><dt>Reason</dt><dd>{flag.reason}</dd></dl></section>
        <section className="card"><h3>Evidence</h3><pre className="evidence">{evidence}</pre></section>
      </div>
      {canDecide ? (
        <form className="card management-form" onSubmit={submit}>
          <h3>Record decision</h3>
          <label className="form-group"><span className="form-label">Decision</span><select className="form-control" value={status} onChange={e => setStatus(e.target.value)}><option value="under_review">Under review</option><option value="dismissed">Dismissed</option><option value="confirmed">Confirmed</option><option value="resolved">Resolve</option></select></label>
          <label className="form-group"><span className="form-label">Note</span><textarea className="form-control" rows="4" maxLength="1000" value={note} onChange={e => setNote(e.target.value)} /></label>
          {validation && <div className="alert alert-danger">{validation}</div>}
          <MutationError error={decision.error} />
          <button className="btn btn-primary" disabled={decision.isPending}>{decision.isPending ? 'Saving…' : 'Save decision'}</button>
        </form>
      ) : <StatePanel title="Read-only review" message="Only Admin and FraudAnalyst roles can change or resolve this flag." />}
    </>
  );
}

