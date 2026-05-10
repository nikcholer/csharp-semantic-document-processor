# Corporate Use Cases

This sample is intentionally small, but the architecture maps to common enterprise document-intake workflows where AI is useful for interpretation and deterministic software is still needed for control.

## Accounts Payable Intake

The current demo mirrors a lightweight accounts-payable queue:

1. receive invoice or receipt image;
2. classify document type;
3. extract structured fields;
4. match vendor or merchant against policy;
5. apply deterministic approval thresholds;
6. return a reviewable decision with reasons and trace metadata.

In a corporate deployment, the same pattern could feed an ERP queue, workflow task, exception report or human approval screen.

## Operations Request Triage

The same shape applies to scanned request forms, claim evidence, delivery notes or incident attachments. The model handles visual interpretation; application code handles validation, routing, permissions, case creation and audit.

## Compliance Review Support

The project demonstrates how an AI extraction step can be made subordinate to deterministic checks. That matters in regulated environments where the system must explain why an item was approved, rejected or escalated.

Useful extensions would include:

- document hashing and provenance;
- tenant-specific policy configuration;
- role-based review queues;
- confidence-based escalation;
- provider-specific cost and latency dashboards;
- secure storage and retention rules.

## Why This Is a Portfolio Project

The goal is to show a practical pattern rather than a generic AI demo: C# owns contracts, policy, validation, logging and tests; the model performs bounded classification and extraction. That is the boundary most enterprise teams need when introducing AI into existing operational systems.
