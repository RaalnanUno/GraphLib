### **WI-XXXX – Integrate GraphLib PDF Runner with EV Attachment Stored Procedure**

**Type:** User Story
**Epic:** EV Modernization – DB2 Retirement / Document Pipeline
**Sprint Target:** TBD (Post-POC / Implementation Sprint)
**Priority:** High

---

### **Title**

Wire GraphLib PDF Runner output to EV database using `p_ins_t_docs_attachments`

---

### **Description**

As part of the EV modernization effort, the GraphLib PDF Runner must persist generated PDF files directly into the EV SQL Server database.

Instead of writing PDFs to disk or SharePoint, the runner will call the stored procedure:

`p_ins_t_docs_attachments`

The procedure will store the PDF as a binary blob along with required metadata.

This task establishes the database integration layer between GraphLib and the EV attachment storage model.

---

### **Stored Procedure Parameters**

| Parameter           | Type   | Description                |
| ------------------- | ------ | -------------------------- |
| `docs_id`           | int    | Unique document identifier |
| `docs_dcty_cd`      | string | Document type code         |
| `docs_last_updt_ts` | string | Last updated timestamp     |
| `docs_file_nm`      | string | File name (no path)        |
| `docs_inst_id`      | string | Source instance ID         |
| `docs_blob_mo`      | bytes  | PDF binary content         |

---

### **Technical Scope**

1. Extend PDF Runner pipeline to:

   * Capture generated PDF as `byte[]`
   * Extract file name
   * Generate or receive `docs_id`
   * Capture/update timestamp

2. Implement database access layer:

   * Use parameterized SQL command
   * Execute `p_ins_t_docs_attachments`
   * Handle connection string via configuration
   * Ensure async-safe execution

3. Add:

   * Structured logging
   * Failure handling (DB unavailable, SP failure)
   * Retry logic (if required by standards)

4. Validate:

   * Correct binary storage
   * Correct metadata persistence
   * File integrity after retrieval

---

### **Acceptance Criteria**

* [ ] PDF Runner successfully calls `p_ins_t_docs_attachments`
* [ ] Stored PDF can be retrieved and opened without corruption
* [ ] Metadata fields populate correctly
* [ ] No duplicate records created
* [ ] Errors are logged with meaningful diagnostics
* [ ] Integration tested against EV SQL Server environment

---

### **Out of Scope**

* Refactoring of stored procedure
* DB schema changes
* UI retrieval logic
* Batch processing optimization

---

### **Dependencies**

* Confirm stored procedure signature
* Confirm connection string access method
* Confirm docs_id generation strategy (DB vs app)

---

### **Risk Considerations**

* Large file size memory handling
* SQL timeout issues
* Blob truncation risks
* Transaction handling consistency

