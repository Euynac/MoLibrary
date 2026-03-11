window.markdownDocsPrototypeData = {
  defaultGroupId: 'framework-manual',
  defaultDocId: 'audit-logging',
  groups: [
    {
      id: 'framework-manual',
      name: 'Framework Manual',
      badge: '10.1 latest',
      description: 'Core platform modules, infrastructure guides, and operational patterns.',
      tree: [
        {
          type: 'folder',
          id: 'get-started',
          label: 'Get Started',
          children: [
            { type: 'doc', docId: 'platform-overview' }
          ]
        },
        {
          type: 'folder',
          id: 'tutorials',
          label: 'Tutorials',
          children: [
            { type: 'doc', docId: 'distributed-locking' }
          ]
        },
        {
          type: 'folder',
          id: 'framework-root',
          label: 'Framework',
          children: [
            {
              type: 'folder',
              id: 'framework-infrastructure',
              label: 'Infrastructure',
              children: [
                { type: 'doc', docId: 'audit-logging' },
                { type: 'doc', docId: 'correlation-id' }
              ]
            }
          ]
        }
      ]
    },
    {
      id: 'ai-operations',
      name: 'AI Operations',
      badge: 'Pilot collection',
      description: 'RAG operations, prompt discipline, and AI workflow notes.',
      tree: [
        {
          type: 'folder',
          id: 'rag-operations',
          label: 'RAG Operations',
          children: [
            { type: 'doc', docId: 'rag-indexing-flow' }
          ]
        },
        {
          type: 'folder',
          id: 'governance',
          label: 'Governance',
          children: [
            { type: 'doc', docId: 'prompt-governance' }
          ]
        }
      ]
    }
  ],
  documents: {
    'platform-overview': {
      id: 'platform-overview',
      groupId: 'framework-manual',
      title: 'Platform Overview',
      eyebrow: 'Get Started',
      summary: 'A quick orientation for how Monica modules compose, register, and surface UI features without forcing the entire framework into every app.',
      updated: 'Updated 5 days ago',
      readingTime: '6 min read',
      version: '10.1 latest',
      path: ['Get Started', 'Platform Overview'],
      meta: [
        { label: 'Audience', value: 'Application builders' },
        { label: 'Focus', value: 'Composition and modularity' },
        { label: 'Status', value: 'Stable guidance' }
      ],
      note: 'Use this page as the first stop before wiring module-specific options. It explains the library shape, not module edge cases.',
      sections: [
        {
          id: 'modular-shape',
          level: 2,
          title: 'Modular Shape',
          paragraphs: [
            'Monica is designed as a library of composable modules. Each module owns its own service registration, options, and optional UI surface.',
            'The practical consequence is simple: users should be able to adopt one concern without inheriting the weight of the rest of the framework.'
          ]
        },
        {
          id: 'registration-flow',
          level: 2,
          title: 'Registration Flow',
          paragraphs: [
            'Registration begins from the module guide, where configuration and dependencies are declared in one place.',
            'UI modules remain optional. They can attach pages and navigation entries without changing the infrastructure contracts beneath them.'
          ]
        },
        {
          id: 'operational-posture',
          level: 2,
          title: 'Operational Posture',
          paragraphs: [
            'The platform prefers explicit options, predictable defaults, and strong separation between UI orchestration and infrastructure behavior.'
          ],
          points: [
            'UI services may use `Res` or `Res<T>` when the view layer consumes failure state directly.',
            'Infrastructure services should return direct values and throw meaningful exceptions.',
            'Hosted services should expose observability when state transitions matter operationally.'
          ]
        }
      ]
    },
    'distributed-locking': {
      id: 'distributed-locking',
      groupId: 'framework-manual',
      title: 'Distributed Locking',
      eyebrow: 'Tutorials',
      summary: 'Prevent duplicate work across nodes by centralizing ownership of critical sections and long-running orchestration steps.',
      updated: 'Updated yesterday',
      readingTime: '8 min read',
      version: '10.1 latest',
      path: ['Tutorials', 'Distributed Locking'],
      meta: [
        { label: 'Audience', value: 'Service operators' },
        { label: 'Focus', value: 'Concurrency safety' },
        { label: 'Pattern', value: 'Lease-based coordination' }
      ],
      note: 'Treat locks as ownership markers, not as a substitute for idempotent business logic.',
      sections: [
        {
          id: 'choose-a-lock-boundary',
          level: 2,
          title: 'Choose a Lock Boundary',
          paragraphs: [
            'Lock the smallest unit that preserves correctness. Coarse locks are easy to reason about but expensive in throughput.',
            'A good boundary usually aligns with a business resource, not an HTTP endpoint or UI action.'
          ]
        },
        {
          id: 'lease-renewal',
          level: 2,
          title: 'Lease Renewal',
          paragraphs: [
            'Long-running work should renew its lease before expiry and emit heartbeat-level state when operational visibility matters.'
          ]
        },
        {
          id: 'failure-recovery',
          level: 2,
          title: 'Failure Recovery',
          paragraphs: [
            'Crash recovery depends on lease expiry and safe re-entry, so the guarded work should remain resumable or idempotent.'
          ]
        }
      ]
    },
    'audit-logging': {
      id: 'audit-logging',
      groupId: 'framework-manual',
      title: 'Audit Logging',
      eyebrow: 'Framework / Infrastructure',
      summary: 'An audit trail captures request, mutation, timing, and exception context so operators can reconstruct what changed and why.',
      updated: 'Updated 2 days ago',
      readingTime: '9 min read',
      version: '10.1 latest',
      path: ['Framework', 'Infrastructure', 'Audit Logging'],
      meta: [
        { label: 'Providers', value: 'EF Core, MongoDB*' },
        { label: 'Entry scope', value: 'Request and service calls' },
        { label: 'Retention', value: 'Configurable' }
      ],
      note: 'Startup templates wire a baseline audit pipeline automatically. Extend the defaults only when you need contributor-level filtering, provider-specific storage, or stricter operational retention.',
      sections: [
        {
          id: 'database-provider-support',
          level: 2,
          title: 'Database Provider Support',
          paragraphs: [
            'The Entity Framework Core provider supports the full audit object flow, including entity change details and request-scoped metadata.',
            'MongoDB support can still persist the broader audit envelope, but entity change capture depends on the shape of the persistence integration.'
          ],
          points: [
            'Use EF Core when entity delta visibility is a hard operational requirement.',
            'Treat provider differences as a storage capability issue, not a UI concern.'
          ]
        },
        {
          id: 'use-auditing',
          level: 2,
          title: 'UseAuditing()',
          paragraphs: [
            'The middleware should be placed in the ASP.NET Core pipeline so each request can establish and persist an audit envelope.',
            'If your application started from a framework template, the middleware is typically already added in the expected order.'
          ],
          codeTitle: 'Pipeline sketch',
          code: [
            'app.UseAuthentication();',
            'app.UseAuditing();',
            'app.UseAuthorization();'
          ]
        },
        {
          id: 'app-auditing-options',
          level: 2,
          title: 'AppAuditingOptions',
          paragraphs: [
            'Application-level options decide which requests are recorded, which contributors run, and how much detail becomes part of the audit object.',
            'This is where noisy traffic should be excluded before persistence costs accumulate.'
          ],
          points: [
            'Ignore high-volume health probes unless they serve a compliance need.',
            'Prefer explicit contributor registration for sensitive enrichments.',
            'Keep request filtering rules deterministic and easy to explain.'
          ]
        },
        {
          id: 'audit-log-object',
          level: 2,
          title: 'Audit Log Object',
          paragraphs: [
            'A single audit object usually includes request metadata, execution timing, user context, exceptions, and application service activity.',
            'The object should answer three questions quickly: who acted, what changed, and how the system responded.'
          ]
        },
        {
          id: 'audit-log-contributors',
          level: 3,
          title: 'Audit Log Contributors',
          paragraphs: [
            'Contributors enrich the audit object with domain-specific facts such as tenant identity, deployment ring, request source, or correlation context.',
            'They should be small, deterministic, and safe to execute for every audited request.'
          ]
        },
        {
          id: 'the-audit-logging-module',
          level: 2,
          title: 'The Audit Logging Module',
          paragraphs: [
            'The module should stay operationally boring: predictable options, clear extension points, and straightforward storage behavior.',
            'When the design is correct, operators rarely think about the module until they need to answer a hard question during an incident or review.'
          ]
        }
      ]
    },
    'correlation-id': {
      id: 'correlation-id',
      groupId: 'framework-manual',
      title: 'Correlation ID',
      eyebrow: 'Framework / Infrastructure',
      summary: 'Carry a stable request identifier across layers so logs, jobs, and audit entries can be traced as one narrative.',
      updated: 'Updated 6 hours ago',
      readingTime: '5 min read',
      version: '10.1 latest',
      path: ['Framework', 'Infrastructure', 'Correlation ID'],
      meta: [
        { label: 'Audience', value: 'Operators and developers' },
        { label: 'Focus', value: 'Traceability' },
        { label: 'Scope', value: 'Request and async hops' }
      ],
      note: 'A correlation identifier is not a user-facing ID. Its job is operational traceability across boundaries.',
      sections: [
        {
          id: 'request-entry',
          level: 2,
          title: 'Request Entry',
          paragraphs: [
            'The identifier should be created or adopted as close to the request edge as possible so downstream systems inherit the same context.'
          ]
        },
        {
          id: 'async-propagation',
          level: 2,
          title: 'Async Propagation',
          paragraphs: [
            'Background work and outbound integrations should carry the same identifier whenever the operation still belongs to the same narrative.'
          ]
        },
        {
          id: 'log-and-audit-join',
          level: 2,
          title: 'Log and Audit Join',
          paragraphs: [
            'The identifier becomes most valuable when logs, audit entries, and domain events all expose it in a predictable field.'
          ]
        }
      ]
    },
    'rag-indexing-flow': {
      id: 'rag-indexing-flow',
      groupId: 'ai-operations',
      title: 'RAG Indexing Flow',
      eyebrow: 'RAG Operations',
      summary: 'Shape a predictable ingestion pipeline so documents become searchable without losing version, source, or embedding context.',
      updated: 'Updated 4 days ago',
      readingTime: '7 min read',
      version: 'Pilot collection',
      path: ['RAG Operations', 'RAG Indexing Flow'],
      meta: [
        { label: 'Pipeline', value: 'Ingest -> chunk -> embed -> persist' },
        { label: 'Audience', value: 'AI operations' },
        { label: 'Status', value: 'Prototype guide' }
      ],
      note: 'The most expensive indexing mistakes happen before embedding starts: bad source hygiene, unstable identifiers, or unclear document ownership.',
      sections: [
        {
          id: 'source-normalization',
          level: 2,
          title: 'Source Normalization',
          paragraphs: [
            'Normalize titles, source paths, and logical owners before chunking so later debug screens can explain where a chunk came from.'
          ]
        },
        {
          id: 'chunk-boundaries',
          level: 2,
          title: 'Chunk Boundaries',
          paragraphs: [
            'Chunk boundaries should preserve semantic continuity. A chunk that starts or ends mid-thought is expensive at retrieval time.'
          ]
        },
        {
          id: 'embedding-consistency',
          level: 2,
          title: 'Embedding Consistency',
          paragraphs: [
            'Knowledge bases should not silently mix embeddings from incompatible models. Track the chosen model as first-class metadata.'
          ]
        }
      ]
    },
    'prompt-governance': {
      id: 'prompt-governance',
      groupId: 'ai-operations',
      title: 'Prompt Governance',
      eyebrow: 'Governance',
      summary: 'Treat system prompts as governed assets with review, lineage, and rollback rather than as ad hoc text blobs.',
      updated: 'Updated 1 week ago',
      readingTime: '6 min read',
      version: 'Pilot collection',
      path: ['Governance', 'Prompt Governance'],
      meta: [
        { label: 'Audience', value: 'Prompt maintainers' },
        { label: 'Focus', value: 'Change discipline' },
        { label: 'Risk', value: 'Behavior drift' }
      ],
      note: 'Prompt quality usually degrades through uncontrolled accumulation, not through one dramatic mistake.',
      sections: [
        {
          id: 'single-owner',
          level: 2,
          title: 'Single Owner',
          paragraphs: [
            'Every prompt set should have a clear owner who can approve changes and explain why the prompt exists in its current form.'
          ]
        },
        {
          id: 'review-before-release',
          level: 2,
          title: 'Review Before Release',
          paragraphs: [
            'Operational prompts deserve the same review discipline as configuration changes: intent, expected behavior, and rollback plan.'
          ]
        },
        {
          id: 'observability-of-drift',
          level: 2,
          title: 'Observability of Drift',
          paragraphs: [
            'Track prompt version alongside conversations and tool calls so shifts in behavior can be tied back to a concrete revision.'
          ]
        }
      ]
    }
  }
};
