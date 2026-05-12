/**
 * @NApiVersion 2.1
 * @NScriptType UserEventScript
 */
define(['N/record', 'N/runtime', 'N/search', 'N/log'], 
function (record, runtime, search, log) {

    function afterSubmit(context) {
        try {
            if (context.type !== context.UserEventType.CREATE &&
                context.type !== context.UserEventType.EDIT) {
                return;
            }

            const rec = context.newRecord;

            // *** UPDATE FIELD ID OF SALES ROLE ***
            const SALESROLE_FIELD = 'salesrole';      
            const salesRole = rec.getValue(SALESROLE_FIELD);

            // Check if employee is CSM
            if (!(salesRole == "1" || salesRole == "CSM")) {
                log.debug("Not CSM", "Employee is not Salesrole 1 or CSM");
                return;
            }

            const employeeId = rec.id;

            // Get current month & year
            const today = new Date();
            const monthName = today.toLocaleString('default', { month: 'short' }); // e.g. "Sep"
            const yearNum = today.getFullYear();

            // *** UPDATE FIELD IDs IN churn customer record ***
            const CHURN_REC_TYPE = 'customrecord_churn_customer';
            const FIELD_EMP = 'custrecord_sales_rep';
            const FIELD_MONTH = 'custrecord_month';
            const FIELD_YEAR = 'custrecord_year';

            // Check if record already exists for employee + month + year
            const existing = search.create({
                type: CHURN_REC_TYPE,
                filters: [
                    [FIELD_EMP, "anyof", employeeId],
                    "AND",
                    [FIELD_MONTH, "is", monthName],
                    "AND",
                    [FIELD_YEAR, "equalto", yearNum]
                ],
                columns: ['internalid']
            }).run().getRange({ start: 0, end: 1 });

            if (existing && existing.length > 0) {
                log.debug("Exists", "Churn record already exists for this CSM & month");
                return;
            }

            // Create new churn customer record
            const churnRec = record.create({
                type: CHURN_REC_TYPE,
                isDynamic: true
            });

            churnRec.setValue(FIELD_EMP, employeeId);
            churnRec.setValue(FIELD_MONTH, monthName);
            churnRec.setValue(FIELD_YEAR, yearNum);

            const newId = churnRec.save();
            log.audit("Created", "Churn Customer record created ID: " + newId);

        } catch (e) {
            log.error("Error in afterSubmit", e);
        }
    }

    return {
        afterSubmit: afterSubmit
    };
});
