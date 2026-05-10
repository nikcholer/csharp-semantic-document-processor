# Security Considerations

This project is a portfolio slice, not a production finance system, but it is structured around the security and governance questions an enterprise team would need to answer before adopting multimodal document processing.

## Trust Boundary

The uploaded image crosses from a caller-controlled boundary into the API. The API validates file type, extension and size before passing the image to the semantic workflow. A production deployment would add malware scanning, tenant-specific upload limits, durable storage rules, retention policy and content provenance.

The model receives the image only for classification and field extraction. Business policy remains in C# services and Semantic Kernel native plugins, so the model does not decide approval, vendor status or threshold handling.

## Prompt Injection and Document Content

Invoices and receipts can contain adversarial text. The workflow treats extracted content as data, not instructions. The model is asked to classify and extract fields, while the deterministic layer evaluates those fields against known policy.

Production hardening would include:

- rejecting unsupported document classes;
- logging extraction confidence and parse failures;
- using allow-listed output schemas;
- reviewing suspicious instructions embedded in source documents;
- adding human review for high-value, unknown-vendor or malformed cases.

## Data Exposure

The demo uses synthetic assets. Real deployments would need data classification rules before using external model providers. Sensitive supplier, payment, personal or customer data should be handled under a clear model-hosting and retention policy.

The provider boundary is concentrated in configuration and model-facing services. That makes it possible to swap between hosted APIs, private endpoints or internally approved model gateways without changing the API contract or policy code.

## Auditability

Responses include typed extracted data, deterministic policy results, correlation IDs and token usage where the provider supplies it. These signals support review, cost analysis and incident investigation.

For production, add durable audit storage with:

- original source reference;
- document hash;
- model/provider/version;
- prompt or prompt version;
- extracted fields;
- policy decision and reasons;
- reviewer overrides where applicable.
