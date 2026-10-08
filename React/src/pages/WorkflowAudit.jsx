import { useQuery } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { getWorkflow, getWorkflowAudit, getWorkflows } from '../services/api';
import { EmptyState, ErrorState, LoadingSpinner, StatusBadge } from '../components/Shared';

export default function WorkflowAudit() {
  const { id } = useParams();
  const query = useQuery({
    queryKey: id ? ['workflow', id] : ['workflows'],
    queryFn: async () => (await (id ? getWorkflow(id) : getWorkflows())).data,
    refetchInterval: id ? 10_000 : false,
  });
  const audit = useQuery({
    queryKey: ['workflow-audit', id],
    queryFn: async () => (await getWorkflowAudit(id)).data,
    enabled: Boolean(id),
    refetchInterval: 10_000,
  });

  if (query.isPending) return <div className="page-content"><LoadingSpinner /></div>;
  if (query.isError) return <div className="page-content"><ErrorState error={query.error} onRetry={query.refetch} /></div>;

  if (!id) {
    return <div className="page-content"><h2>Workflow executions</h2>
      {query.data.length === 0 ? <EmptyState message="No workflow executions found." /> :
        <div className="table-wrapper"><table><thead><tr><th>Execution</th><th>Type</th><th>Subject</th><th>Status</th><th>Updated</th></tr></thead>
          <tbody>{query.data.map(item => <tr key={item.workflowExecutionId}>
            <td><Link to={`/workflows/${item.workflowExecutionId}`}>{item.workflowExecutionId}</Link></td>
            <td>{item.subjectType === 'purchase_request' ? 'Purchase request' : 'Claim'}</td>
            <td>{item.purchaseRequestId ? `PR #${item.purchaseRequestId}` : `Claim #${item.expenseClaimId}`}</td>
            <td><StatusBadge status={item.status} /></td>
            <td>{new Date(item.updatedAt).toLocaleString()}</td>
          </tr>)}</tbody></table></div>}
    </div>;
  }

  const workflow = query.data;
  return <div className="page-content">
    <div className="page-heading"><div><h2>Workflow execution</h2><code>{workflow.workflowExecutionId}</code></div>
      <button className="btn btn-ghost btn-sm" disabled={query.isFetching} onClick={() => { query.refetch(); audit.refetch(); }}>Refresh</button></div>
    <div className="card"><StatusBadge status={workflow.status} /><p>{workflow.objective}</p>
      <p>{workflow.subjectType === 'purchase_request' ? `Purchase request #${workflow.purchaseRequestId}` : `Claim #${workflow.expenseClaimId}`}</p>
      <p>Correlation ID: <code>{workflow.correlationId}</code></p></div>
    <h3>Steps</h3>
    {workflow.steps.length === 0 ? <EmptyState message="No execution steps recorded." /> :
      <div className="workflow-steps">{workflow.steps.map(step => <div className="card" key={step.workflowStepId}>
        <strong>{step.sequence}. {step.name}</strong> <StatusBadge status={step.status} />
        <p>{step.type}</p>{step.error && <div className="alert alert-danger">{step.error}</div>}
        {step.validationResults?.map(result => <p key={result.validationResultId}>{result.validator}: {result.isValid ? 'Valid' : 'Invalid'}</p>)}
        {step.toolExecutions?.map(tool => <p key={tool.toolExecutionId}>Tool {tool.toolName}: {tool.status}</p>)}
      </div>)}</div>}
    <h3>Audit trail</h3>
    {audit.isPending && <LoadingSpinner />}
    {audit.isError && <ErrorState error={audit.error} onRetry={audit.refetch} />}
    {audit.data?.length === 0 && <EmptyState message="No audit events recorded." />}
    {audit.data?.map(event => <div className="card" key={event.auditLogId}>
      <strong>{event.action}</strong> · {new Date(event.createdAt).toLocaleString()}
      <p>{event.entityType} {event.entityId}</p>
    </div>)}
  </div>;
}
