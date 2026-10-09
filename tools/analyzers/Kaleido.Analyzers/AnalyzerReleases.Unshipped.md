### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-----------------------------
KAL2001 | Kaleido.Usage | Error | [ProcessStep] Version, DisplayName, and Description must be non-empty
KAL2002 | Kaleido.Usage | Error | [QuerySource] Version, DisplayName, and Description must be non-empty
KAL2003 | Kaleido.Usage | Error | [QueryView] Version, DisplayName, and Description must be non-empty
KAL2004 | Kaleido.Usage | Warning | Step handler catch (Exception) must filter OperationCanceledException
KAL2005 | Kaleido.Usage | Warning | ServiceName must be lowercase with no spaces or separators
KAL2008 | Kaleido.Usage | Warning | IProcessStep type has no IProcessStepHandler in this compilation
KAL2009 | Kaleido.Usage | Warning | AddKaleido() options lambda does not set Assemblies
KAL2010 | Kaleido.Usage | Error | [ProcessStep] type must implement IProcessStep
KAL2011 | Kaleido.Usage | Error | IProcessStep type must have [ProcessStep]
KAL2012 | Kaleido.Usage | Error | [QuerySource]/[QueryView] type must implement the matching interface
KAL2013 | Kaleido.Usage | Error | Query source/view must have [QuerySource]/[QueryView]
KAL2014 | Kaleido.Usage | Warning | [Filterable]/[Searchable]/[Sortable] only apply to IQueryContext records
KAL2015 | Kaleido.Usage | Error | An information step declares only InformationRequestId and Items
KAL2016 | Kaleido.Usage | Error | Information steps are required with RequireInformation, not Success
KAL2017 | Kaleido.Usage | Warning | Process step input property has no description
KAL2018 | Kaleido.Usage | Warning | Query context or parameters property has no description
