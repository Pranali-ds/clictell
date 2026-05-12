/**
 * @NApiVersion 2.1
 * @NScriptType ScheduledScript
 */

define(['N/search', 'N/record', 'N/log'], function (search, record, log) {

    function execute(context) {

        try {
            log.audit('START', 'Customer Growth Monthly Backfill started');

            // 1️⃣ Get distinct CSM + Customer + Year combinations
            var growthSearch = search.create({
                type: 'customrecord_customer_growth',
                filters: [],
                columns: [
                    search.createColumn({ name: 'custrecord_cust_grw_csm', summary: 'GROUP' }),
                    search.createColumn({ name: 'custrecord_cust_grw_customer', summary: 'GROUP' }),
                    search.createColumn({ name: 'custrecord_cust_grw_year', summary: 'GROUP' })
                ]
            });

            growthSearch.run().each(function (res) {

                var csmId = res.getValue({ name: 'custrecord_cust_grw_csm', summary: 'GROUP' });
                var customerId = res.getValue({ name: 'custrecord_cust_grw_customer', summary: 'GROUP' });
                var yearId = res.getValue({ name: 'custrecord_cust_grw_year', summary: 'GROUP' });

                processMonths(csmId, customerId, yearId);

                return true;
            });

            log.audit('END', 'Customer Growth Monthly Backfill completed');

        } catch (e) {
            log.error('SCHEDULE ERROR', e);
        }
    }

    /* ============================================================
       CREATE / FIX MONTHLY RECORDS
    ============================================================ */

    function processMonths(csmId, customerId, yearId) {

        let previousMonthRevenue = 0;

        for (let month = 1; month <= 12; month++) {

            // Check existing record
            let existing = search.create({
                type: 'customrecord_customer_growth',
                filters: [
                    ['custrecord_cust_grw_csm', 'anyof', csmId],
                    'AND', ['custrecord_cust_grw_customer', 'anyof', customerId],
                    'AND', ['custrecord_cust_grw_month', 'anyof', month],
                    'AND', ['custrecord_cust_grw_year', 'anyof', yearId]
                ],
                columns: [
                    'internalid',
                    'custrecord_cust_grw_monthly_revenue'
                ]
            }).run().getRange({ start: 0, end: 1 });

            let rec;
            let monthlyRevenue = 0;

            if (existing.length) {
                rec = record.load({
                    type: 'customrecord_customer_growth',
                    id: existing[0].getValue('internalid')
                });

                monthlyRevenue = parseFloat(
                    rec.getValue('custrecord_cust_grw_monthly_revenue')
                ) || 0;
            } else {
                rec = record.create({ type: 'customrecord_customer_growth' });

                rec.setValue('custrecord_cust_grw_csm', csmId);
                rec.setValue('custrecord_cust_grw_customer', customerId);
                rec.setValue('custrecord_cust_grw_month', month);
                rec.setValue('custrecord_cust_grw_year', yearId);
                rec.setValue('custrecord_cust_grw_monthly_revenue', 0);
            }

            // 🔹 Increment = M1 - M0 (even if 0)
            let increment = monthlyRevenue - previousMonthRevenue;

            rec.setValue('custrecord_cust_grw_monthly_increment', increment);

            let savedId = rec.save();
            log.audit('MONTH PROCESSED', {
                csm: csmId,
                customer: customerId,
                month: month,
                revenue: monthlyRevenue,
                increment: increment,
                recordId: savedId
            });

            previousMonthRevenue = monthlyRevenue;
        }
    }

    return {
        execute: execute
    };
});
