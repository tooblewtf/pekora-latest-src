import { createUseStyles } from "react-jss";
import { getTheme, themeType } from "../../services/theme";

const footerLinks = {
  '/about-us': 'About Us',
  'https://discord.gg/': 'Discord',
  '/internal/robuxexchange': 'Robux Exchange',
  '/internal/tixexchange': 'Tix Exchange',
  '/auth/tos': 'Terms',
  '/auth/privacy': 'Privacy',
};

const useFooterStyles2 = createUseStyles({
  footerContainer:{
    padding: '12px',
    background: p => p.theme === themeType.obc2016 ? '#393939' : 'var(--white-color)',
    width: '100%',
    marginTop: '40px',
    boxShadow: '0 0 3px rgba(25, 25, 25, 0.3)',
  },
  footer:{
    textAlign: 'center',
    margin: '0 auto',
    maxWidth: '970px',
    display: 'flex',
    flexDirection: 'column',
  },
  footerLinks:{
    padding: 0,
    textAlign: 'center',
    marginBottom: '20px',
    marginTop: '20px',
    display: 'flex',
    flexWrap: 'wrap',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginLeft: 0,
    marginRight: 0,
    listStyle: 'none',
    '&:before,&:after':{
      content: " ",
      display: 'table',
    }
  },
  footerLink:{
    margin: '6px',
    whiteSpace: 'nowrap',
    listStyle: 'none',
    '& a':{
      fontSize: '16px',
      fontWeight: '500',
      color: p => p.theme === themeType.obc2016 ? '#var(--text-color-secondary-dark)' : 'var(--text-color-secondary)',
      textDecoration: 'none',
      '&:hover':{
        textDecoration: 'none',
        color: 'var(--text-color-primary)',
      }
    }
  },
  footerNote:{
    //borderTop: '1px solid var(--background-color)',
    fontSize: '10px',
    fontWeight: '500',
    margin: '12px auto',
    textAlign: 'center',
    width: '78%',
    color: p => p.theme === themeType.obc2016 ? '#var(--text-color-secondary-dark)' : 'var(--text-color-secondary)',
    lineHeight: '1.5em',
    wordWrap: 'break-word',
    hyphens: 'none',
  },
});

const Footer = props => {
  const s = useFooterStyles2({ theme: getTheme() });
  return <footer className={s.footerContainer}>
    <div className={s.footer}>
      <ul className={s.footerLinks}>
        {
          Object.getOwnPropertyNames(footerLinks).map(v => {
            return <li className={s.footerLink}>
            <a href={v}> {footerLinks[v]} </a>
          </li>
          })
        }
      </ul>
      <p className={s.footerNote}>©2025 Marine. Marine is not affliated with Roblox Corporation.</p>
    </div>
  </footer>

  /*return <footer className={s.footer}>
    <div className={'container mt-4 mb-0 ' + s.footerContainer}>
      <div className={'row ' + s.footerRow}>
        {
          Object.getOwnPropertyNames(footerLinks).map(v => {
            return <div className='col-2 mb-2' key={v}>
              <h2 className={s.text + ' ' + s.link}>
                <a className={s.text + ' ' + s.link} href={v}>{footerLinks[v]}</a>
              </h2>
            </div>
          })
        }
        <div className={'col-12 col-lg-10 ' + s.lowerFooterContainer}>
          <p className={`${s.text} ${s.text2}`}>
            <a>©2025 Marine. Marine is not affliated with Roblox Corporation.</a>.
          </p>
        </div>
      </div>
    </div>
  </footer>*/
}

export default Footer;