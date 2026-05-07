const form = document.querySelector("#uploadForm");
const imageInput = document.querySelector("#imageInput");
const sourceIdInput = document.querySelector("#sourceId");
const dropZone = document.querySelector("#dropZone");
const selectedFile = document.querySelector("#selectedFile");
const processButton = document.querySelector("#processButton");
const healthDot = document.querySelector("#healthDot");
const healthText = document.querySelector("#healthText");
const resultTitle = document.querySelector("#resultTitle");
const statusPill = document.querySelector("#statusPill");
const categoryMetric = document.querySelector("#categoryMetric");
const decisionMetric = document.querySelector("#decisionMetric");
const tokensMetric = document.querySelector("#tokensMetric");
const extractedData = document.querySelector("#extractedData");
const policyReasons = document.querySelector("#policyReasons");
const rawJson = document.querySelector("#rawJson");

const fieldLabels = {
  vendorName: "Vendor",
  invoiceNumber: "Invoice number",
  totalAmount: "Total",
  taxAmount: "Tax",
  invoiceDate: "Invoice date",
  storeName: "Store",
  purchaseDate: "Purchase date",
  paymentMethod: "Payment method",
  currencyCode: "Currency"
};

checkHealth();

imageInput.addEventListener("change", () => {
  const file = imageInput.files?.[0];
  selectedFile.textContent = file ? `${file.name} (${formatBytes(file.size)})` : "No file selected";
});

["dragenter", "dragover"].forEach((eventName) => {
  dropZone.addEventListener(eventName, (event) => {
    event.preventDefault();
    dropZone.classList.add("dragging");
  });
});

["dragleave", "drop"].forEach((eventName) => {
  dropZone.addEventListener(eventName, (event) => {
    event.preventDefault();
    dropZone.classList.remove("dragging");
  });
});

dropZone.addEventListener("drop", (event) => {
  const file = event.dataTransfer.files?.[0];
  if (!file) {
    return;
  }

  const transfer = new DataTransfer();
  transfer.items.add(file);
  imageInput.files = transfer.files;
  imageInput.dispatchEvent(new Event("change"));
});

form.addEventListener("submit", async (event) => {
  event.preventDefault();

  const file = imageInput.files?.[0];
  if (!file) {
    renderError({
      code: "missing_file",
      message: "Choose a PNG or JPEG document image before processing.",
      target: "image",
      traceId: "-"
    });
    return;
  }

  const body = new FormData();
  body.append("image", file);

  const sourceId = sourceIdInput.value.trim();
  if (sourceId.length > 0) {
    body.append("sourceId", sourceId);
  }

  setBusy(true);

  try {
    const response = await fetch("/api/documents/process", {
      method: "POST",
      headers: {
        "X-Correlation-ID": crypto.randomUUID()
      },
      body
    });
    const payload = await response.json();

    if (!response.ok) {
      renderError(payload);
      return;
    }

    renderSuccess(payload);
  } catch (error) {
    renderError({
      code: "request_failed",
      message: error instanceof Error ? error.message : "The request failed.",
      target: null,
      traceId: "-"
    });
  } finally {
    setBusy(false);
  }
});

async function checkHealth() {
  try {
    const response = await fetch("/health");
    const payload = await response.json();
    healthDot.classList.toggle("ready", response.ok);
    healthDot.classList.toggle("failed", !response.ok);
    healthText.textContent = response.ok
      ? `${payload.aiProvider} / ${payload.aiModel}`
      : "API unavailable";
  } catch {
    healthDot.classList.add("failed");
    healthText.textContent = "API unavailable";
  }
}

function setBusy(isBusy) {
  processButton.disabled = isBusy;
  processButton.querySelector("span").textContent = isBusy ? "Processing..." : "Process document";
  if (isBusy) {
    resultTitle.textContent = "Processing document";
    statusPill.textContent = "Running";
    statusPill.className = "status-pill";
  }
}

function renderSuccess(payload) {
  const document = payload.document;
  const policy = document?.policyResult;
  const decision = policy?.decision ?? "N/A";

  resultTitle.textContent = `${payload.category} processed`;
  categoryMetric.textContent = payload.category ?? "-";
  decisionMetric.textContent = decision;
  tokensMetric.textContent = payload.modelUsage?.totalTokens ?? "-";
  statusPill.textContent = decision === "N/A" ? "Complete" : decision;
  statusPill.className = `status-pill ${decision === "Approved" ? "approved" : "review"}`;

  renderData(document?.data ?? {});
  renderReasons(policy?.reasons ?? payload.warnings ?? []);
  rawJson.textContent = JSON.stringify(payload, null, 2);
}

function renderError(error) {
  resultTitle.textContent = "Request failed";
  categoryMetric.textContent = "-";
  decisionMetric.textContent = error.code ?? "Error";
  tokensMetric.textContent = "-";
  statusPill.textContent = "Error";
  statusPill.className = "status-pill error";

  renderData({
    code: error.code ?? "request_failed",
    target: error.target ?? "-",
    traceId: error.traceId ?? "-"
  });
  renderReasons([error.message ?? "The request failed."]);
  rawJson.textContent = JSON.stringify(error, null, 2);
}

function renderData(data) {
  const entries = Object.entries(data);
  extractedData.replaceChildren();

  if (entries.length === 0) {
    extractedData.appendChild(createDataRow("Document", "No extracted fields returned"));
    return;
  }

  for (const [key, value] of entries) {
    extractedData.appendChild(createDataRow(fieldLabels[key] ?? sentenceCase(key), formatValue(value)));
  }
}

function renderReasons(reasons) {
  policyReasons.replaceChildren();
  const items = reasons.length > 0 ? reasons : ["No policy reasons returned."];

  for (const reason of items) {
    const item = document.createElement("li");
    item.textContent = reason;
    policyReasons.appendChild(item);
  }
}

function createDataRow(label, value) {
  const wrapper = document.createElement("div");
  const term = document.createElement("dt");
  const description = document.createElement("dd");
  term.textContent = label;
  description.textContent = value;
  wrapper.append(term, description);
  return wrapper;
}

function formatValue(value) {
  if (value === null || value === undefined || value === "") {
    return "-";
  }

  if (typeof value === "number") {
    return Number.isInteger(value) ? value.toString() : value.toFixed(2);
  }

  return String(value);
}

function sentenceCase(value) {
  return value
    .replace(/([A-Z])/g, " $1")
    .replace(/^./, (letter) => letter.toUpperCase())
    .trim();
}

function formatBytes(bytes) {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  const kilobytes = bytes / 1024;
  if (kilobytes < 1024) {
    return `${kilobytes.toFixed(1)} KB`;
  }

  return `${(kilobytes / 1024).toFixed(2)} MB`;
}
