/**
 * @NApiVersion 2.1
 * @NScriptType MapReduceScript
 *
 * Final script with:
 *  - Minimal audit logging only
 *  - Execution time monitor (overall + per-reduce)
 *  - Half-year write only to June (6) and December (12)
 *
 * All other logic preserved, with historical opening for SEP-2024 computed from commission records
 */

define(['N/search', 'N/log', 'N/format', 'N/record', 'N/runtime'], function (search, log, format, record, runtime) {

    /* ============================================================
       CONSTANT FIELD IDS (unchanged)
       ============================================================ */
    const RECORD_TYPE = 'customrecord_churn_customer';

    const FIELD_EMP            = 'custrecord_sales_rep';
    const FIELD_MONTH          = 'custrecord_month';
    const FIELD_YEAR           = 'custrecord_year';
    const FIELD_NEW            = 'custrecord_new_customer';
    const FIELD_CHURN          = 'custrecord_churn_customer';
    const FIELD_OPENING        = 'custrecord_opening_balance';
    const FIELD_CLOSING        = 'custrecord_closing_balance';
    const FIELD_MONTHLY_AVG    = 'custrecord_monthly_average_churn';
    const FIELD_PRIMARY        = 'custrecord_primary_month';
    const FIELD_HALF_YEAR      = 'custrecord_half_yearly_churn';
    const FIELD_KPI            = 'custrecord_kpi_achievement';
    const FIELD_H1_H2          = 'custrecord_h1_h2';

    /* ============================================================
       YEAR MAP
       ============================================================ */
    const YEAR_MAP = {
        "2020": 1,  "2021": 2,  "2022": 3,  "2023": 4,
        "2024": 5,  "2025": 6,  "2026": 7,  "2027": 8,
        "2028": 9,  "2029": 10, "2030": 11, "2031": 12,
        "2032": 13, "2033": 14, "2034": 15, "2035": 16,
        "2036": 17, "2037": 18, "2038": 19, "2039": 20,
        "2040": 21
    };

    /* ============================================================
       MONTH MAPPINGS
       ============================================================ */
    const MONTH_ABBR_TO_NUM = {
        "JAN": 1, "FEB": 2, "MAR": 3, "APR": 4, "MAY": 5, "JUN": 6,
        "JUL": 7, "AUG": 8, "SEP": 9, "OCT": 10, "NOV": 11, "DEC": 12
    };

    const MONTH_NUM_TO_ABBR = {
        1: "JAN", 2: "FEB", 3: "MAR", 4: "APR", 5: "MAY", 6: "JUN",
        7: "JUL", 8: "AUG", 9: "SEP", 10: "OCT", 11: "NOV", 12: "DEC"
    };

    function nextMonth(m, y) {
        if (m === "DEC") return { month: "JAN", year: (parseInt(y) + 1).toString() };
        return {
            month: Object.keys(MONTH_ABBR_TO_NUM)[Object.values(MONTH_ABBR_TO_NUM).indexOf(MONTH_ABBR_TO_NUM[m]) + 1],
            year: y
        };
    }

    function n(v) {
        let f = parseFloat(v);
        return isFinite(f) ? f : 0;
    }

    /* ============================================================
       KPI LOGIC
       ============================================================ */
    function kpiFromAvg(avg) {
        // if (avg < 2) return 125;
        // if (avg >= 2 && avg < 5) return 100;
        // if (avg >= 5 && avg < 10) return 75;
        // if (avg >= 10 && avg < 15) return 50;
        // return 0;
        if (avg < 1) return 125;
        if (avg >= 1 && avg < 2) return 100;
        if (avg >= 2 && avg < 3) return 50;
        if (avg >= 3 && avg < 5) return 0;
        if (avg >= 5 && avg < 10) return -25;
        return -50;

    }

    /* ============================================================
       EXECUTION / METRICS (global trackers)
       ============================================================ */
    let _mrStartTime = null;
    let _inputRows = 0;
    let _reduceCount = 0;
    let _rowsInserted = 0; // create/save occurrences
    let _rowsUpdated = 0;  // submitFields occurrences
    let _reduceDurations = []; // ms per reduce

    /* ============================================================
       GET INPUT DATA
       ============================================================ */
    function getInputData() {

        _mrStartTime = Date.now();

        let customersData = {};

        let startDate = new Date(2024, 8, 1); // SEP 2024
        let today = new Date();
        let endDate = new Date(today.getFullYear(), today.getMonth(), today.getDate());

        let loop = new Date(startDate);

        /* ============================================================
           NEW CUSTOMERS
           ============================================================ */
        while (loop <= endDate) {

            let mStart = new Date(loop.getFullYear(), loop.getMonth(), 1);
            let mEnd = new Date(loop.getFullYear(), loop.getMonth() + 1, 0);

            let fStart = format.format({ value: mStart, type: format.Type.DATE });
            let fEnd = format.format({ value: mEnd, type: format.Type.DATE });

            let mAbbr = MONTH_NUM_TO_ABBR[mStart.getMonth() + 1];
            let keyMonYear = mAbbr + "-" + mStart.getFullYear();

            let newCustSearch = search.create({
                type: "customrecord_commission",
                filters: [
                    ["custrecord_commission_emp.salesrole", "anyof", "1"], "AND",
                    ["custrecord_commission_inv.mainline", "is", "T"], "AND",
                    ["custrecord_commission_customer.custentity_cls_in_customer", "anyof", "12", "22", "5", "27", "25"], "AND",
                    ["custrecord_commission_customer.firstsaledate", "within", fStart, fEnd]
                ],
                columns: [
                    search.createColumn({ name: "custrecord_commission_emp", summary: "GROUP" }),
                    search.createColumn({ name: "custrecord_commission_customer", summary: "COUNT" })
                ]
            });

            newCustSearch.run().each(r => {
                let emp = r.getValue({ name: "custrecord_commission_emp", summary: "GROUP" });
                let cnt = n(r.getValue({ name: "custrecord_commission_customer", summary: "COUNT" }));

                let key = emp + " - " + keyMonYear;
                if (!customersData[key])
                    customersData[key] = { csmEmp: emp, monthYear: keyMonYear, newCustomersCount: 0, churnCustomersCount: 0 };

                customersData[key].newCustomersCount += cnt;
                return true;
            });

            loop = new Date(loop.getFullYear(), loop.getMonth() + 1, 1);
        }

        /* ============================================================
           CHURN CUSTOMERS (ALLOCATE TO NEXT MONTH)
           ============================================================ */
        let cStart = format.format({ value: startDate, type: format.Type.DATE });
        let cEnd = format.format({ value: endDate, type: format.Type.DATE });

        let churnSearch = search.create({
            type: "customrecord_commission",
            filters: [
                ["custrecord_commission_inv.mainline", "is", "T"], "AND",
                ["custrecord_commission_emp.salesrole", "anyof", "1"], "AND",
                ["custrecord_commission_customer.lastsaledate", "within", cStart, cEnd], "AND",
                ["custrecord_commission_customer.custentity_cls_in_customer", "anyof", "12", "22", "5", "27", "25"]
            ],
            columns: [
                search.createColumn({ name: "custrecord_commission_emp", summary: "GROUP" }),
                search.createColumn({ name: "custrecord_commission_customer", summary: "COUNT" }),
                search.createColumn({
                    name: "formulatext",
                    summary: "GROUP",
                    formula: "TO_CHAR({custrecord_commission_customer.lastsaledate},'MON') || '-' || TO_CHAR({custrecord_commission_customer.lastsaledate},'YYYY')"
                })
            ]
        });

        churnSearch.run().each(r => {

            let emp = r.getValue({ name: "custrecord_commission_emp", summary: "GROUP" });
            let cnt = n(r.getValue({ name: "custrecord_commission_customer", summary: "COUNT" }));

            let raw = r.getValue({ name: "formulatext", summary: "GROUP" });
            let [mon, yr] = raw.split("-");

            mon = mon.toUpperCase();
            yr = yr.toString();

            let nxt = nextMonth(mon, yr);

            let keyMonYear = nxt.month + "-" + nxt.year;
            let key = emp + " - " + keyMonYear;

            if (!customersData[key])
                customersData[key] = { csmEmp: emp, monthYear: keyMonYear, newCustomersCount: 0, churnCustomersCount: 0 };

            customersData[key].churnCustomersCount += cnt;

            return true;
        });

        _inputRows = Object.values(customersData).length;

        // Minimal audit log: MR started + input row count
        log.audit({
            title: 'MR START',
            details: 'Start: ' + new Date(_mrStartTime).toISOString() + ' | Input rows: ' + _inputRows
        });

        return Object.values(customersData);
    }

    /* ============================================================
       MAP
       ============================================================ */
    function map(context) {
        let o = JSON.parse(context.value);
        let key = o.csmEmp + "-" + o.monthYear;
        context.write(key, o);
    }

    /* ============================================================
       REDUCE
       ============================================================ */
    function reduce(context) {

        let reduceStart = Date.now();

        _reduceCount++;

        // Minimal audit: reduce start for employee key
        log.audit({
            title: 'REDUCE START',
            details: 'Key: ' + context.key + ' | Start: ' + new Date(reduceStart).toISOString()
        });

        /* Merge incoming values */
        let merged = { csmEmp: null, monthYear: null, newCustomersCount: 0, churnCustomersCount: 0 };

        context.values.forEach(v => {
            let o = JSON.parse(v);
            merged.csmEmp = merged.csmEmp || o.csmEmp;
            merged.monthYear = merged.monthYear || o.monthYear;
            merged.newCustomersCount += n(o.newCustomersCount);
            merged.churnCustomersCount += n(o.churnCustomersCount);
        });

        let emp = merged.csmEmp;
        let [monAbbr, yearStr] = merged.monthYear.split("-");
        let monNum = MONTH_ABBR_TO_NUM[monAbbr];
        let yearId = YEAR_MAP[yearStr];

        // ------------------------------------------------------------
        // PREVENT CREATION OF FUTURE MONTH RECORDS
        // ------------------------------------------------------------
        // If the target month-year is strictly in the future relative
        // to the bundle execution date, skip creating/updating the record.
        let today = new Date();
        let curYear = today.getFullYear();
        let curMonth = today.getMonth() + 1; // 1..12

        let rYear = parseInt(yearStr, 10);
        let rMonth = monNum;

        if (rYear > curYear || (rYear === curYear && rMonth > curMonth)) {
            log.audit({
                title: 'SKIPPED FUTURE MONTH',
                details: 'Skipping future month ' + monAbbr + '-' + yearStr + ' for Emp: ' + emp + ' | ReduceKey: ' + context.key
            });
            return;
        }
        // ------------------------------------------------------------

        /* ============================================================
           UPSERT single monthly row
           ============================================================ */
        let existingId = null;

        search.create({
            type: RECORD_TYPE,
            filters: [
                [FIELD_EMP, "anyof", emp], "AND",
                [FIELD_MONTH, "anyof", monNum], "AND",
                [FIELD_YEAR, "anyof", yearId]
            ],
            columns: ["internalid"]
        }).run().each(r => {
            existingId = r.getValue("internalid");
            return false;
        });

        if (!existingId) {
            let rec = record.create({ type: RECORD_TYPE });
            rec.setValue({ fieldId: FIELD_EMP, value: emp });
            rec.setValue({ fieldId: FIELD_MONTH, value: monNum });
            rec.setValue({ fieldId: FIELD_YEAR, value: yearId });
            rec.setValue({ fieldId: FIELD_NEW, value: merged.newCustomersCount });
            rec.setValue({ fieldId: FIELD_CHURN, value: merged.churnCustomersCount });
            rec.setValue({ fieldId: FIELD_H1_H2, value: (monNum <= 6 ? 1 : 2) });
            try {
                existingId = rec.save();
                _rowsInserted++;
            } catch (e) {
                // Minimal audit: error during save
                log.audit({
                    title: 'REDUCE SAVE ERROR',
                    details: 'Key: ' + context.key + ' | Error: ' + (e && e.message ? e.message : e)
                });
                throw e;
            }
        } else {
            record.submitFields({
                type: RECORD_TYPE,
                id: existingId,
                values: {
                    [FIELD_NEW]: merged.newCustomersCount,
                    [FIELD_CHURN]: merged.churnCustomersCount,
                    [FIELD_H1_H2]: (monNum <= 6 ? 1 : 2)
                }
            });
            _rowsUpdated++;
        }

        /* ============================================================
           PROCESS ALL MONTHS FOR EMP — OPENING/CLOSING
           ============================================================ */
        let monthlyRows = [];

        let s = search.create({
            type: RECORD_TYPE,
            filters: [[FIELD_EMP, "anyof", emp]],
            columns: [
                "internalid", FIELD_YEAR, FIELD_MONTH, FIELD_NEW, FIELD_CHURN,
                FIELD_OPENING, FIELD_CLOSING, FIELD_PRIMARY
            ]
        }).run();

        s.each(r => {
            let id = r.getValue("internalid");

            let yVal = r.getValue(FIELD_YEAR);
            let mVal = r.getValue(FIELD_MONTH);
            let mNum = parseInt(mVal);

            monthlyRows.push({
                id: id,
                year: parseInt(Object.keys(YEAR_MAP).find(k => YEAR_MAP[k] == yVal)),
                monthNum: mNum,
                monthAbbr: MONTH_NUM_TO_ABBR[mNum],
                newCount: n(r.getValue(FIELD_NEW)),
                churnCount: n(r.getValue(FIELD_CHURN)),
                openingExisting: n(r.getValue(FIELD_OPENING)),
                closingExisting: n(r.getValue(FIELD_CLOSING)),
                primary: (r.getValue(FIELD_PRIMARY) === 'T')
            });

            return true;
        });

        // --- NEW: compute historical opening for SEP-2024 if needed ---
        // We'll compute only if this employee has a row for SEP-2024 (month 9, year 2024)
        let historicalOpening = 0;
        let needsHistoricalSepOpening = monthlyRows.some(r => r.monthNum === 9 && r.year === 2024);

        if (needsHistoricalSepOpening) {
            try {
                // Count distinct customers acquired BEFORE Sep 1, 2024
                let acquiredSearch = search.create({
                    type: "customrecord_commission",
                    filters: [
                        ["custrecord_commission_emp", "anyof", emp], "AND",
                        ["custrecord_commission_customer.firstsaledate", "before", "9/1/2024"], "AND",
                        ["custrecord_commission_customer.custentity_cls_in_customer", "anyof", "12", "22", "5", "27", "25"]
                    ],
                    columns: [
                        search.createColumn({
                            name: "custrecord_commission_customer",
                            summary: "COUNT"
                        })
                    ]
                }).run();

                let acquiredRange = acquiredSearch.getRange({ start: 0, end: 1 });
                let acquired = 0;
                if (acquiredRange && acquiredRange.length > 0) {
                    acquired = n(acquiredRange[0].getValue({ name: "custrecord_commission_customer", summary: "COUNT" }));
                }

                // Count distinct customers whose last sale is BEFORE Sep 1, 2024 (i.e., churned before Sep)
                let churnedSearch = search.create({
                    type: "customrecord_commission",
                    filters: [
                        ["custrecord_commission_emp", "anyof", emp], "AND",
                        ["custrecord_commission_customer.lastsaledate", "before", "9/1/2024"], "AND",
                        ["custrecord_commission_customer.custentity_cls_in_customer", "anyof", "12", "22", "5", "27", "25"]
                    ],
                    columns: [
                        search.createColumn({
                            name: "custrecord_commission_customer",
                            summary: "COUNT"
                        })
                    ]
                }).run();

                let churnRange = churnedSearch.getRange({ start: 0, end: 1 });
                let churnBefore = 0;
                if (churnRange && churnRange.length > 0) {
                    churnBefore = n(churnRange[0].getValue({ name: "custrecord_commission_customer", summary: "COUNT" }));
                }

                historicalOpening = acquired - churnBefore;
                if (historicalOpening < 0) historicalOpening = 0;

                log.audit({
                    title: "Historical Opening (AUG-2024) computed",
                    details: "Emp: " + emp + " | Acquired(before Sep): " + acquired + " | Churned(before Sep): " + churnBefore + " | Opening: " + historicalOpening
                });
            } catch (e) {
                log.audit({
                    title: "Historical Opening Error",
                    details: "Emp: " + emp + " | Error: " + (e && e.message ? e.message : e)
                });
                // proceed — will fall back to default logic if this fails
                historicalOpening = 0;
            }
        }
        // --- END historical opening compute ---

        if (monthlyRows.length > 0) {

            monthlyRows.sort((a, b) => {
                if (a.year !== b.year) return a.year - b.year;
                return a.monthNum - b.monthNum;
            });

            /* Find primary opening */
            let opening = 0;

            // If there's a SEP-2024 row and we computed a historical opening, prefer that
            if (needsHistoricalSepOpening && historicalOpening > 0) {
                opening = historicalOpening;
            } else {
                let primaryRow = monthlyRows.find(r => r.primary && r.closingExisting > 0);
                if (primaryRow) opening = primaryRow.closingExisting;
                else if (monthlyRows[0].openingExisting > 0) opening = monthlyRows[0].openingExisting;
            }

            /* Sequentially compute opening/closing */
            let prevClosing = null;

            monthlyRows.forEach(r => {
                let openVal = (prevClosing !== null) ? prevClosing : opening;

                // If this is the SEP-2024 row and openingExisting is 0 but historicalOpening exists, use it
                if (r.monthNum === 9 && r.year === 2024 && opening === 0 && historicalOpening > 0) {
                    openVal = historicalOpening;
                }

                let closeVal = openVal + r.newCount - r.churnCount;
                if (!isFinite(closeVal)) closeVal = 0;
                closeVal = Math.max(0, parseFloat(closeVal.toFixed(2)));

                let monthlyAvg = 0;
              if (openVal > 0) monthlyAvg = parseFloat((r.churnCount / openVal).toFixed(2));

                record.submitFields({
                    type: RECORD_TYPE,
                    id: r.id,
                    values: {
                        [FIELD_OPENING]: openVal,
                        [FIELD_CLOSING]: closeVal,
                        [FIELD_MONTHLY_AVG]: monthlyAvg
                    }
                });

                _rowsUpdated++;

                prevClosing = closeVal;
            });
        }

        /* ============================================================
           HALF-YEAR + KPI (ONLY write to June (6) and Dec (12))
           ============================================================ */
        let rowsByYear = {};

        monthlyRows.forEach(r => {
            if (!rowsByYear[r.year]) rowsByYear[r.year] = [];
            rowsByYear[r.year].push(r);
        });

        Object.keys(rowsByYear).forEach(y => {

            let yearRows = rowsByYear[y];

            let fresh = {};
            yearRows.forEach(r => {
                let lk = search.lookupFields({
                    type: RECORD_TYPE,
                    id: r.id,
                    columns: [FIELD_MONTHLY_AVG, FIELD_OPENING, FIELD_CHURN, FIELD_MONTH]
                });

                let mNum = parseInt(lk[FIELD_MONTH]) || r.monthNum;

                fresh[mNum] = {
                    id: r.id,
                    opening: n(lk[FIELD_OPENING]),
                    avg: n(lk[FIELD_MONTHLY_AVG]),
                    churn: n(lk[FIELD_CHURN])
                };
            });

            function computeHalfYear(monthArr, lastMonthNumber) {
                let sum = 0, count = 0, repId = null;

                monthArr.forEach(m => {
                    if (!fresh[m]) return;
                    let o = fresh[m];

                    if (o.opening > 0) {
                        let a = o.avg > 0 ? o.avg : (o.churn / o.opening);
                        sum += a;
                        count++;
                    }

                    if (repId === null) repId = o.id;
                });

                if (count === 0 || repId === null) return;

                let avg = parseFloat((sum / count).toFixed(2));
                let kpi = kpiFromAvg(avg);

                // **Only write to the last month of the half (June -> 6, December -> 12)**
                if (fresh[lastMonthNumber]) {
                    record.submitFields({
                        type: RECORD_TYPE,
                        id: fresh[lastMonthNumber].id,
                        values: {
                            [FIELD_HALF_YEAR]: avg + "%",
                            [FIELD_KPI]: kpi + "%"
                        }
                    });
                    _rowsUpdated++;
                }
            }

            // H1 -> write only on month 6 (June)
            computeHalfYear([1, 2, 3, 4, 5, 6], 6);

            // H2 -> write only on month 12 (December)
            computeHalfYear([7, 8, 9, 10, 11, 12], 12);
        });

        let reduceEnd = Date.now();
        let durationMs = reduceEnd - reduceStart;
        _reduceDurations.push(durationMs);

        // Minimal audit: reduce end with duration
        log.audit({
            title: 'REDUCE END',
            details: 'Key: ' + context.key + ' | Duration(ms): ' + durationMs
        });

    }

    /* ============================================================
       SUMMARIZE
       ============================================================ */
    function summarize(summary) {

        let mrEndTime = Date.now();
        let totalDurationMs = mrEndTime - (_mrStartTime || mrEndTime);
        let avgReduce = _reduceDurations.length > 0 ? Math.round(_reduceDurations.reduce((a, b) => a + b, 0) / _reduceDurations.length) : 0;

        // Minimal audit summary
        log.audit({
            title: 'MR SUMMARY',
            details:
                'Start: ' + new Date(_mrStartTime).toISOString()
                + ' | End: ' + new Date(mrEndTime).toISOString()
                + ' | TotalDuration(ms): ' + totalDurationMs
                + ' | InputRows: ' + _inputRows
                + ' | EmployeesProcessed: ' + _reduceCount
                + ' | RowsInserted: ' + _rowsInserted
                + ' | RowsUpdated: ' + _rowsUpdated
                + ' | AvgReduceDuration(ms): ' + avgReduce
        });
    }

    return {
        getInputData,
        map,
        reduce,
        summarize
    };

});