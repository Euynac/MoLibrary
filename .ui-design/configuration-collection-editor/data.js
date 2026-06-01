window.collectionEditorData = {
  definition: {
    displayName: 'Docs Portal Demo',
    definitionKey: 'docs.portal.demo',
    nodePath: 'Services',
    schemaVersion: 3
  },
  services: [
    {
      key: 'billing',
      displayName: 'Billing Service',
      enabled: true,
      baseUrl: 'https://billing.internal.monica.local',
      timeoutSeconds: 12,
      deploymentSlot: 'Staging',
      tags: ['payments', 'critical'],
      connectedDbs: [
        {
          key: 'main',
          role: 'Primary',
          provider: 'SqlServer',
          connectionString: 'Server=billing-sql;Database=Billing;',
          maxPoolSize: 120,
          readOnly: false
        },
        {
          key: 'readonly',
          role: 'Read Replica',
          provider: 'SqlServer',
          connectionString: 'Server=billing-sql-ro;Database=Billing;',
          maxPoolSize: 80,
          readOnly: true
        }
      ]
    },
    {
      key: 'search',
      displayName: 'Search Service',
      enabled: true,
      baseUrl: 'https://search.internal.monica.local',
      timeoutSeconds: 6,
      deploymentSlot: 'Production',
      tags: ['read-heavy'],
      connectedDbs: [
        {
          key: 'catalog',
          role: 'Catalog Index',
          provider: 'Postgres',
          connectionString: 'Host=search-pg;Database=Catalog;',
          maxPoolSize: 60,
          readOnly: true
        }
      ]
    },
    {
      key: 'notifications',
      displayName: 'Notifications',
      enabled: false,
      baseUrl: 'https://notify.internal.monica.local',
      timeoutSeconds: 20,
      deploymentSlot: 'Canary',
      tags: ['async', 'email'],
      connectedDbs: [
        {
          key: 'queue',
          role: 'Outbox',
          provider: 'Sqlite',
          connectionString: 'Data Source=notify-outbox.db',
          maxPoolSize: 20,
          readOnly: false
        }
      ]
    }
  ],
  rawJson: {
    Services: {
      billing: {
        DisplayName: 'Billing Service',
        Enabled: true,
        BaseUrl: 'https://billing.internal.monica.local',
        TimeoutSeconds: 12,
        DeploymentSlot: 'Staging',
        ConnectedDbs: [
          {
            Key: 'main',
            Role: 'Primary',
            Provider: 'SqlServer',
            ConnectionString: 'Server=billing-sql;Database=Billing;',
            MaxPoolSize: 120,
            ReadOnly: false
          },
          {
            Key: 'readonly',
            Role: 'Read Replica',
            Provider: 'SqlServer',
            ConnectionString: 'Server=billing-sql-ro;Database=Billing;',
            MaxPoolSize: 80,
            ReadOnly: true
          }
        ]
      }
    }
  }
};
