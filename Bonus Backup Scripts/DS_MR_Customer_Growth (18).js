/**
 * @NApiVersion 2.1
 * @NScriptType MapReduceScript
 */
define(['N/search','N/record','N/log'], function (search, record, log) {

    var MONTHS = [1,2,3,4,5,6,7,8,9,10,11,12];
    var SPECIAL_MONTHS = [6, 12];

    /* ============================================================
       GET INPUT DATA
    ============================================================ */
    function getInputData() {
        return search.create({
            type: 'invoice',
            filters: [
                ['type','anyof','CustInvc'],
                'AND',['custrecord_commission_inv.internalid','noneof','@NONE@'],
                'AND',['custrecord_commission_inv.custrecord_sales_role','anyof','2'],
                'AND',['mainline','is','T']
            ],
            columns: [
                search.createColumn({
                    name:'custrecord_commission_emp',
                    join:'CUSTRECORD_COMMISSION_INV',
                    summary:'GROUP'
                }),
                search.createColumn({ name:'entity', summary:'GROUP' }),
                search.createColumn({
                    name:'formulatext',
                    summary:'GROUP',
                    formula:"TO_CHAR({trandate},'MM')||'-'||TO_CHAR({trandate},'YYYY')"
                }),
                search.createColumn({ name:'amount', summary:'SUM' })
            ]
        });
    }

    /* ============================================================
       MAP
    ============================================================ */
    function map(context) {

        var r = JSON.parse(context.value);
        var parts = r.values['GROUP(formulatext)'].split('-');

        context.write({
            key:
                r.values['GROUP(custrecord_commission_emp.CUSTRECORD_COMMISSION_INV)'].value +
                '_' +
                r.values['GROUP(entity)'].value +
                '_' +
                parts[1],
            value: {
                csm: r.values['GROUP(custrecord_commission_emp.CUSTRECORD_COMMISSION_INV)'].value,
                customer: r.values['GROUP(entity)'].value,
                month: parseInt(parts[0], 10),
                year: parseInt(parts[1], 10),
                revenue: parseFloat(r.values['SUM(amount)']) || 0
            }
        });
    }

    /* ============================================================
       REDUCE
    ============================================================ */
    function reduce(context) {

        var rows = context.values.map(JSON.parse);
        var base = rows[0];

        var csmId = base.csm;
        var customerId = base.customer;
        var year = base.year;
        var yearId = getYearInternalId(year.toString());

        /* ---------------- Month Map ---------------- */
        var monthMap = {};
        var invoiceMonths = new Set();

        MONTHS.forEach(m => {
            monthMap[m] = {
                revenue: 0,
                increment: 0,
                newCustRevenue: 0
            };
        });

        rows.forEach(r => {
            monthMap[r.month].revenue = r.revenue;
            invoiceMonths.add(r.month);
        });

        /* ---------------- Monthly Increment ---------------- */
        let lastPositiveRevenue = null;

        MONTHS.forEach(m => {
            var rev = monthMap[m].revenue;
            if (rev > 0) {
                monthMap[m].increment =
                    lastPositiveRevenue === null ? 0 : rev - lastPositiveRevenue;
                lastPositiveRevenue = rev;
            }
        });

        /* ---------------- New Customer Revenue ---------------- */
        var firstRevenueMonth = MONTHS.find(
            m => monthMap[m].revenue > 0
        );

        if (firstRevenueMonth) {
            monthMap[firstRevenueMonth].newCustRevenue =
                monthMap[firstRevenueMonth].revenue;
        }

        /* ---------------- Half-Year Aggregates ---------------- */
        var H1Revenue = sumRange(monthMap, 1, 6, 'revenue');
        var H2Revenue = sumRange(monthMap, 7, 12, 'revenue');

        var H1Increment = sumRange(monthMap, 1, 6, 'increment');
        var H2Increment = sumRange(monthMap, 7, 12, 'increment');

        /* ---------------- Save Records ---------------- */
        MONTHS.forEach(m => {

            var shouldCreate =
                invoiceMonths.has(m) || SPECIAL_MONTHS.includes(m);

            if (!shouldCreate) return;

            var rec = getOrCreateGrowthRecord(
                csmId, customerId, m, yearId
            );

            rec.setValue('custrecord_cust_grw_csm', csmId);
            rec.setValue('custrecord_cust_grw_customer', customerId);
            rec.setValue('custrecord_cust_grw_month', m);
            rec.setValue('custrecord_cust_grw_year', yearId);
            rec.setValue('custrecord_cust_grw_monthly_revenue', monthMap[m].revenue);
            rec.setValue('custrecord_cust_grw_monthly_increment', monthMap[m].increment);
            rec.setValue(
                'custrecord_cust_grw_new_customer_revenue',
                monthMap[m].newCustRevenue
            );

            if (m === 6 || m === 12) {
                var halfRevenue = (m === 6) ? H1Revenue : H2Revenue;
                var halfIncrement = (m === 6) ? H1Increment : H2Increment;

                rec.setValue('custrecord_cust_grw_half_yearly_revenue', halfRevenue);
                rec.setValue('custrecord_cust_grw_half_yearly_monthly', halfIncrement);

                var avgGrowth =
                    halfRevenue ? (halfIncrement / halfRevenue) : 0;

                rec.setValue(
                    'custrecord_csm_average_customer_growth',
                    avgGrowth
                );

                /* ✅ NEW FIELD ADDED (ONLY CHANGE) */
                var avgRevenueGrow =
                    halfRevenue ? (halfIncrement / halfRevenue) : 0;

                rec.setValue(
                    'custrecord_cust_grw_average_revenue_grow',
                    avgRevenueGrow
                );
            }

            rec.save();
        });
    }

    /* ============================================================
       HELPERS
    ============================================================ */

    function sumRange(map, start, end, field) {
        let total = 0;
        for (let i = start; i <= end; i++) {
            total += map[i][field] || 0;
        }
        return total;
    }

    function getOrCreateGrowthRecord(csm, customer, month, yearId) {
        var res = search.create({
            type:'customrecord_customer_growth',
            filters:[
                ['custrecord_cust_grw_csm','anyof',csm],
                'AND',['custrecord_cust_grw_customer','anyof',customer],
                'AND',['custrecord_cust_grw_month','anyof',month],
                'AND',['custrecord_cust_grw_year','anyof',yearId]
            ],
            columns:['internalid']
        }).run().getRange({ start:0, end:1 });

        return res.length
            ? record.load({
                type:'customrecord_customer_growth',
                id:res[0].getValue('internalid')
              })
            : record.create({ type:'customrecord_customer_growth' });
    }

    function getYearInternalId(yearText) {
        var res = search.create({
            type:'customlist_year',
            filters:[['name','is',yearText]],
            columns:['internalid']
        }).run().getRange({ start:0, end:1 });

        return res.length ? res[0].getValue('internalid') : null;
    }

    return {
        getInputData,
        map,
        reduce
    };
});