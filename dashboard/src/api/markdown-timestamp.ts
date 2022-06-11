import moment from 'moment'

const timestampRegex = /<t:(\d{10})(:[tTdDfFR])?>/g
const parseTimestamps = (html: string) => {
  return html.replaceAll(
    timestampRegex,
    (match, p1, p2) => {
      const timestamp = moment.unix(parseInt(p1))
      const format: string = p2 ? p2[1] : 'f'
      if (format !== 'R') {
        let dateTimeFormatOptions = {
          t: 'HH:mm',
          T: 'HH:mm:ss',
          d: 'DD/MM/YYYY',
          D: 'DD MMMM YYYY',
          f: 'DD MMMM YYYY HH:mm',
          F: 'dddd, DD MMMM YYYY HH:mm',
        }[format]
        return timestamp.format(dateTimeFormatOptions)
      }
      return timestamp.fromNow()
    },
  )
}

export default parseTimestamps
