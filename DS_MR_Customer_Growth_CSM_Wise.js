/**
 * @NApiVersion 2.1
 * @NScriptType MapReduceScript
 */
define(['N/search', 'N/record', 'N/log'], function (search, record, log) {

    const SUMMARY_RECORD_TYPE = 'customrecord_customer_growth_summary';

    /* ============================================================
       GET INPUT DATA – Saved Search Logic
    ============================================================ */
    function getInputData() {

        log.audit('STEP', 'Customer Growth Summary Search Started');

        return search.create({
            type: 'customrecord_customer_growth',
            filters: [
                ['isinactive', 'is', 'F']
            ],
            columns: [
                search.createColumn({
                    name: 'custrecord_cust_grw_csm',
                    summary: 'GROUP'
                }),
                search.createColumn({
                    name: 'custrecord_cust_grw_customer',
                    summary: 'GROUP'
                }),
                search.createColumn({
                    name: 'custrecord_cust_grw_year',
                    summary: 'GROUP'
                }),
                search.createColumn({
                    name: 'custrecord_cust_grw_half_yearly_montly',
                    summary: 'SUM'
                }),
                search.createColumn({
                    name: 'formulanumeric',
                    summary: 'SUM',
                    formula:
                        "CASE " +
                        "WHEN SUM({custrecord_cust_grw_new_customer_revenue}) = 0 " +
                        "OR SUM({custrecord_cust_grw_half_yearly_montly}) = 0 " +
                        "THEN 0 " +
                        "ELSE ROUND(" +
                        "SUM({custrecord_cust_grw_new_customer_revenue}) / " +
                        "SUM({custrecord_cust_grw_half_yearly_montly}), 3) " +
                        "END"
                }),
                search.createColumn({
                    name: 'formulapercent',
                    summary: 'MAX',
                    formula:
                        "CASE " +
                        "WHEN AVG({custrecord_cust_grw_average_revenue_grw}) >= 25 THEN 125 " +
                        "WHEN AVG({custrecord_cust_grw_average_revenue_grw}) >= 10 THEN 100 " +
                        "WHEN AVG({custrecord_cust_grw_average_revenue_grw}) >= 7.5 THEN 75 " +
                        "WHEN AVG({custrecord_cust_grw_average_revenue_grw}) >= 2.5 THEN 50 " +
                        "WHEN AVG({custrecord_cust_grw_average_revenue_grw}) >= 0 THEN 0 " +
                        "ELSE -25 END"
                })
            ]
        });
    }

    /* ============================================================
       MAP – Key = CSM|Year|Customer
    ============================================================ */
    function map(context) {

        var result = JSON.parse(context.value);

        var csmId = result.values.custrecord_cust_grw_csm?.value;
        var customerId = result.values.custrecord_cust_grw_customer?.value;
        var yearId = result.values.custrecord_cust_grw_year?.value;

        if (!csmId || !customerId || !yearId) return;

        var key = csmId + '|' + yearId + '|' + customerId;

        context.write({
            key: key,
            value: {
                csm: csmId,
                customer: customerId,
                year: yearId,
                halfYearSum: parseFloat(result.values.custrecord_cust_grw_half_yearly_montly) || 0,
                avgGrowth: parseFloat(result.values.formulanumeric) || 0,
                kpi: parseFloat(result.values.formulapercent) || 0
            }
        });
    }

    /* ============================================================
       REDUCE – Create OR Update Summary Record
    ============================================================ */
    function reduce(context) {

        var data = JSON.parse(context.values[0]);

        try {
            var existingId = findExistingSummary(
                data.csm,
                data.customer,
                data.year
            );

            var rec = existingId
                ? record.load({
                    type: SUMMARY_RECORD_TYPE,
                    id: existingId
                })
                : record.create({
                    type: SUMMARY_RECORD_TYPE
                });

            rec.setValue('custrecord_csm_summary', data.csm);
            rec.setValue('custrecord_customer_summary', data.customer);
            rec.setValue('custrecord_year_summary', data.year);
            rec.setValue('custrecord_sum_half_yr_month_incream_sum', data.halfYearSum);
            rec.setValue('custrecord_average_customer_growth', data.avgGrowth);

            /* ✅ KPI Achievement */
            rec.setValue('custrecord_cust_grw_kpi', data.kpi);

            var recordId = rec.save();

            log.audit(
                existingId ? 'Updated Summary Record' : 'Created Summary Record',
                recordId
            );

        } catch (e) {
            log.error('Reduce Error - ' + context.key, e);
        }
    }

    /* ============================================================
       FIND EXISTING SUMMARY RECORD
    ============================================================ */
    function findExistingSummary(csmId, customerId, yearId) {

        var summarySearch = search.create({
            type: SUMMARY_RECORD_TYPE,
            filters: [
                ['custrecord_csm_summary', 'anyof', csmId],
                'AND', ['custrecord_customer_summary', 'anyof', customerId],
                'AND', ['custrecord_year_summary', 'anyof', yearId]
            ],
            columns: ['internalid']
        });

        var result = summarySearch.run().getRange({
            start: 0,
            end: 1
        });

        return result.length ? result[0].id : null;
    }

    /* ============================================================
       SUMMARIZE
    ============================================================ */
    function summarize(summary) {

        log.audit('Map/Reduce Completed', {
            usage: summary.usage,
            yields: summary.yields,
            concurrency: summary.concurrency
        });

        summary.reduceSummary.errors.iterator().each(function (key, error) {
            log.error('Reduce Error for Key ' + key, error);
            return true;
        });
    }

    return {
        getInputData: getInputData,
        map: map,
        reduce: reduce,
        summarize: summarize
    };
});
