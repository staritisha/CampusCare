# CampusCare REST Web API Documentation

This document provides the complete API reference for the **CampusCare Web API** (`CampusCare.WebAPI`), including endpoint descriptions, request/response models, query parameters, cURL examples, error codes, and external webhook integrations.

---

## 1. Overview & Swagger UI

- **API Framework**: ASP.NET Core 10.0 Web API
- **Default Base URL (Local Development)**: `http://localhost:5174` or `https://localhost:7168`
- **Interactive Swagger UI**: `http://localhost:5174/` (Mapped directly to root URL)
- **OpenAPI JSON Specification**: `http://localhost:5174/swagger/v1/swagger.json`
- **Data Format**: `application/json`

---

## 2. API Endpoints Summary

### Complaints API (`/api/complaints`)
| HTTP Method | Route | Description |
| :--- | :--- | :--- |
| `GET` | `/api/complaints` | Retrieve complaints with query filters (`status`, `departmentId`, `categoryId`, `studentId`, `staffId`, `search`, `unassignedOnly`, `escalatedOnly`) |
| `GET` | `/api/complaints/{id}` | Retrieve full complaint details, AI triage results, comments, and audit history |
| `POST` | `/api/complaints` | Submit a new complaint (triggers AI triage, auto-categorization, ID generation, and notification) |
| `PUT` | `/api/complaints/{id}/status` | Update complaint status following state machine rules with notes and history |
| `PUT` | `/api/complaints/{id}/assign` | Assign technician, priority, and category to a complaint |
| `POST` | `/api/complaints/{id}/comments` | Add a comment to a complaint |
| `POST` | `/api/complaints/{id}/feedback` | Submit 1–5 star student feedback for resolved complaints |
| `DELETE` | `/api/complaints/{id}` | Permanently delete a complaint record and its associated history |
| `POST` | `/api/complaints/purge` | Bulk purge historical complaints (Closed/Resolved/Rejected) older than specified days |
| `POST` | `/api/complaints/escalate-overdue` | Trigger SLA escalation check for overdue complaints (> 48h) |
| `POST` | `/api/complaints/n8n/webhook-callback`| Receive incoming webhook callbacks from n8n workflows |

### Departments API (`/api/departments`)
| HTTP Method | Route | Description |
| :--- | :--- | :--- |
| `GET` | `/api/departments` | Get list of all departments with staff and categories count |
| `GET` | `/api/departments/{id}` | Get department details by ID |
| `POST` | `/api/departments` | Create a new campus department |
| `PUT` | `/api/departments/{id}` | Update existing department details |
| `DELETE` | `/api/departments/{id}` | Delete a department |
| `GET` | `/api/departments/{id}/staff` | Get list of active staff members in the department |

### Categories API (`/api/categories`)
| HTTP Method | Route | Description |
| :--- | :--- | :--- |
| `GET` | `/api/categories` | Get list of all complaint categories with default department names |
| `GET` | `/api/categories/{id}` | Get category details by ID |
| `POST` | `/api/categories` | Create a new complaint category |
| `PUT` | `/api/categories/{id}` | Update existing category |
| `DELETE` | `/api/categories/{id}` | Delete a category |

### Users & Staff Management API (`/api/users`)
| HTTP Method | Route | Description |
| :--- | :--- | :--- |
| `GET` | `/api/users` | Get all system users with roles, departments, and active statuses |
| `GET` | `/api/users/staff` | Get staff members filtered by department for assignment dropdowns |
| `POST` | `/api/users/staff` | Create a new staff or manager user account |
| `PUT` | `/api/users/{id}/toggle-status` | Toggle user account status (Active / Deactivated) |

### Analytics & Reporting API (`/api/analytics`)
| HTTP Method | Route | Description |
| :--- | :--- | :--- |
| `GET` | `/api/analytics/admin` | Executive KPI counters, average resolution time, feedback score, department/category stats |
| `GET` | `/api/analytics/manager/{departmentId}` | Department KPI metrics and technician workload breakdown |
| `GET` | `/api/analytics/staff/{staffId}` | Technician workdesk KPI counters |
| `GET` | `/api/analytics/student/{studentId}` | Student dashboard complaint counters |

---

## 3. Sample cURL Requests

### Submit a Complaint via API
```bash
curl -X POST "http://localhost:5174/api/complaints" \
     -H "Content-Type: application/json" \
     -d '{
       "title": "Wi-Fi down in Lab 3",
       "description": "Students cannot access online exam system due to disconnected router.",
       "location": "Computer Lab 3",
       "studentId": "student-user-id",
       "studentEmail": "student1@college.com"
     }'
```

### Update Complaint Status
```bash
curl -X PUT "http://localhost:5174/api/complaints/1/status" \
     -H "Content-Type: application/json" \
     -d '{
       "complaintId": 1,
       "newStatus": "InProgress",
       "userId": "staff-user-id",
       "commentText": "Technician on site inspecting router."
     }'
```

### Assign Staff Member
```bash
curl -X PUT "http://localhost:5174/api/complaints/1/assign" \
     -H "Content-Type: application/json" \
     -d '{
       "complaintId": 1,
       "managerId": "manager-user-id",
       "selectedStaffId": "staff-user-id",
       "priority": "High",
       "categoryId": 1,
       "note": "Assigned high priority for exam period."
     }'
```

### Fetch Admin Analytics Summary
```bash
curl -X GET "http://localhost:5174/api/analytics/admin" \
     -H "Accept: application/json"
```
