### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-----------------------------
KAL2001 | Kaleido.Usage | Error | [ProcessStep] Version, DisplayName, and Description must be non-empty
KAL2002 | Kaleido.Usage | Error | [QueryContext] Name and Version must be non-empty
KAL2003 | Kaleido.Usage | Error | [QueryView] Name and Version must be non-empty
KAL2004 | Kaleido.Usage | Warning | Step handler catch (Exception) must filter OperationCanceledException
KAL2005 | Kaleido.Usage | Warning | ServiceName must be lowercase with no spaces or separators
KAL2008 | Kaleido.Usage | Warning | IProcessStep type has no IProcessStepHandler in this compilation
KAL2009 | Kaleido.Usage | Warning | AddKaleido() options lambda does not set Assemblies
KAL2010 | Kaleido.Usage | Error | [ProcessStep] type must implement IProcessStep
KAL2011 | Kaleido.Usage | Error | IProcessStep type must have [ProcessStep]
