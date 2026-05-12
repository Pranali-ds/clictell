/**
 * @NApiVersion 2.1
 * @NScriptType MapReduceScript
 */
define(['N/search','N/record','N/log'], function (search, record, log) {

    const MONTHS = [1,2,3,4,5,6,7,8,9,10,11,12];
    const SPECIAL_MONTHS = [6, 12];

    /* ============================================================
       GET INPUT DATA
    ============================================================ */
    function getInputData() {

        log.audit('GET INPUT', 'Starting invoice search (validated search)');

        return search.create({
            type: "invoice",
            filters: [
                ["type","anyof","CustInvc"],
                "AND",["custrecord_commission_inv.internalid","noneof","@NONE@"],
                "AND",["custrecord_commission_inv.custrecord_commission_emp","anyof","1240","1238","1190"],
                "AND",["custrecord_commission_inv.custrecord_sales_role","anyof","1"],
                "AND",["trandate","within","1/1/2023","12/31/2025"],
                "AND",["mainline","is","T"]
            ],
            columns: [
                search.createColumn({
                    name: "custrecord_commission_emp",
                    join: "CUSTRECORD_COMMISSION_INV",
                    summary: "GROUP"
                }),
                search.createColumn({
                    name: "entity",
                    summary: "GROUP"
                }),
                search.createColumn({
                    name: "formulatext",
                    summary: "GROUP",
                    formula: "TO_CHAR({trandate}, 'MM') || '-' || TO_CHAR({trandate}, 'YYYY')"
                }),
                search.createColumn({
                    name: "amount",
                    summary: "SUM"
                })
            ]
        });
    }

    /* ============================================================
       MAP
    ============================================================ */
    function map(context) {
        try {
            const r = JSON.parse(context.value);
            const parts = r.values['GROUP(formulatext)'].split('-');

            const key =
                r.values['GROUP(custrecord_commission_emp.CUSTRECORD_COMMISSION_INV)'].value +
                '_' +
                r.values['GROUP(entity)'].value +
                '_' +
                parts[1];

            context.write({
                key,
                value: {
                    csm: r.values['GROUP(custrecord_commission_emp.CUSTRECORD_COMMISSION_INV)'].value,
                    customer: r.values['GROUP(entity)'].value,
                    month: parseInt(parts[0], 10),
                    year: parseInt(parts[1], 10),
                    revenue: parseFloat(r.values['SUM(amount)']) || 0
                }
            });

        } catch (e) {
            log.error('MAP ERROR', e);
        }
    }

    /* ============================================================
       REDUCE
    ============================================================ */
    function reduce(context) {

        try {
            log.audit('REDUCE START', context.key);

            const rows = context.values.map(JSON.parse);
            const base = rows[0];

            const csmId = base.csm;
            const customerId = base.customer;
            const yearId = getYearInternalId(base.year.toString());

            const monthMap = {};
            const invoiceMonths = new Set();

            MONTHS.forEach(m => {
                monthMap[m] = { revenue: 0, increment: 0 };
            });

            rows.forEach(r => {
                monthMap[r.month].revenue = r.revenue;
                invoiceMonths.add(r.month);
            });

            let lastPositiveRevenue = null;
            MONTHS.forEach(m => {
                if (monthMap[m].revenue > 0) {
                    monthMap[m].increment =
                        lastPositiveRevenue === null ? 0 : monthMap[m].revenue - lastPositiveRevenue;
                    lastPositiveRevenue = monthMap[m].revenue;
                }
            });

            /* ========= FIX START (ONLY NEW CUSTOMER FIELD) ========= */

            const firstRevenueMonth = MONTHS.find(m => monthMap[m].revenue > 0);
            const alreadyHasNewCustRevenue = hasNewCustomerRevenue(csmId, customerId);

            log.audit('NEW CUSTOMER CHECK', {
                csmId,
                customerId,
                firstRevenueMonth,
                alreadyHasNewCustRevenue
            });

            /* ========= FIX END ========= */

            const H1Revenue = sumRange(monthMap, 1, 6, 'revenue');
            const H2Revenue = sumRange(monthMap, 7, 12, 'revenue');
            const H1Increment = sumRange(monthMap, 1, 6, 'increment');
            const H2Increment = sumRange(monthMap, 7, 12, 'increment');

            MONTHS.forEach(m => {

                if (!invoiceMonths.has(m) && !SPECIAL_MONTHS.includes(m)) return;

                const rec = getOrCreateGrowthRecord(csmId, customerId, m, yearId);

                rec.setValue('custrecord_cust_grw_csm', csmId);
                rec.setValue('custrecord_cust_grw_customer', customerId);
                rec.setValue('custrecord_cust_grw_month', m);
                rec.setValue('custrecord_cust_grw_year', yearId);
                rec.setValue('custrecord_cust_grw_monthly_revenue', monthMap[m].revenue);
                rec.setValue('custrecord_cust_grw_monthly_increment', monthMap[m].increment);

                /* ========= FIXED FIELD ONLY ========= */
                if (!alreadyHasNewCustRevenue && m === firstRevenueMonth) {
                    rec.setValue(
                        'custrecord_cust_grw_new_customer_revenue',
                        monthMap[m].revenue
                    );
                    log.audit('NEW CUSTOMER REVENUE SET', {
                        month: m,
                        value: monthMap[m].revenue
                    });
                } else {
                    rec.setValue('custrecord_cust_grw_new_customer_revenue', 0);
                }
                /* =================================== */

                if (m === 6 || m === 12) {
                    const halfRevenue = m === 6 ? H1Revenue : H2Revenue;
                    const halfIncrement = m === 6 ? H1Increment : H2Increment;
                    const avgRevGrowth = halfRevenue ? (halfIncrement / halfRevenue) : 0;

                    rec.setValue('custrecord_cust_grw_half_yearly_revenue', halfRevenue);
                    rec.setValue('custrecord_cust_grw_half_yearly_montly', halfIncrement);
                    if(avgRevGrowth){
                    rec.setValue('custrecord_cust_grw_average_revenue_grw', avgRevGrowth);
                    }
                }

                rec.save();
            });

        } catch (e) {
            log.error('REDUCE ERROR', e);
        }
    }

    /* ============================================================
       HELPERS
    ============================================================ */

    function hasNewCustomerRevenue(csm, customer) {
        const res = search.create({
            type: 'customrecord_customer_growth',
            filters: [
                ['custrecord_cust_grw_csm','anyof',csm],
                'AND',['custrecord_cust_grw_customer','anyof',customer],
                'AND',['custrecord_cust_grw_new_customer_revenue','greaterthan',0]
            ],
            columns: ['internalid']
        }).run().getRange({ start: 0, end: 1 });

        return res.length > 0;
    }

    function sumRange(map, start, end, field) {
        let total = 0;
        for (let i = start; i <= end; i++) {
            total += map[i][field] || 0;
        }
        return total;
    }

    function getOrCreateGrowthRecord(csm, customer, month, yearId) {
        const res = search.create({
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
            ? record.load({ type:'customrecord_customer_growth', id:res[0].getValue('internalid') })
            : record.create({ type:'customrecord_customer_growth' });
    }

    function getYearInternalId(yearText) {
        const res = search.create({
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